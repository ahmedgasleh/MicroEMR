using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.Patients.Services;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Application.ClinicalOutput;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace MicroEMR.Application.PatientReferrals;

public sealed class PatientReferralService(
    IPatientReferralRepository referrals,
    IPatientRepository patients,
    IAuthenticatedClinicalUserAccessor clinicalUserAccessor,
    IReferralStatusTransitionService transitionService,
    IPatientService? patientService = null,
    IClinicConfigurationService? clinicService = null,
    IReferralDocumentRepository? documentLinks = null,
    IClinicalPrintLayoutRenderer? printLayout = null,
    IPdfRenderer? pdfRenderer = null,
    TimeProvider? timeProvider = null) : IPatientReferralService
{
    public async Task<IReadOnlyList<PatientReferralListItemResponse>> GetByPatientUidAsync(
        Guid patientUid,
        CancellationToken cancellationToken = default)
    {
        await EnsurePatientExistsAsync(patientUid, cancellationToken);
        var results = await referrals.GetByPatientUidAsync(patientUid, cancellationToken);
        return results.Select(MapListItem).ToArray();
    }

    public async Task<PatientReferralDetailsResponse?> GetByUidAsync(
        Guid patientUid,
        Guid referralUid,
        CancellationToken cancellationToken = default)
    {
        await EnsurePatientExistsAsync(patientUid, cancellationToken);
        var referral = await referrals.GetByUidAsync(patientUid, referralUid, cancellationToken);
        return referral is null ? null : MapDetails(referral);
    }

    public async Task<PatientReferralDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientReferralRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        await EnsurePatientExistsAsync(patientUid, cancellationToken);

        var actorId = await clinicalUserAccessor.GetRequiredUserIdAsync(cancellationToken);
        var normalizedRequest = new CreatePatientReferralRequest
        {
            ReferringProviderUid = request.ReferringProviderUid,
            RecipientName = request.RecipientName.Trim(),
            RecipientOrganization = NormalizeOptional(request.RecipientOrganization),
            RecipientPhone = NormalizeOptional(request.RecipientPhone),
            RecipientFax = NormalizeOptional(request.RecipientFax),
            Reason = request.Reason.Trim(),
            ClinicalSummary = NormalizeOptional(request.ClinicalSummary)
        };

        var referral = await referrals.CreateAsync(
            patientUid,
            normalizedRequest,
            actorId,
            cancellationToken);

        return MapDetails(referral);
    }

    public async Task<PatientReferralDetailsResponse?> UpdateDraftAsync(Guid patientUid, Guid referralUid,
        UpdatePatientReferralDraftRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        await EnsurePatientExistsAsync(patientUid, cancellationToken);
        var actorId = await clinicalUserAccessor.GetRequiredUserIdAsync(cancellationToken);
        var updated = await referrals.UpdateDraftAsync(patientUid, referralUid, request, actorId, cancellationToken);
        return updated is null ? null : MapDetails(updated);
    }

    public Task<IReadOnlyList<ReferralProviderListItem>> GetActiveProvidersAsync(CancellationToken cancellationToken = default) =>
        referrals.GetActiveProvidersAsync(cancellationToken);

    public async Task<byte[]?> PreviewLetterAsync(Guid patientUid, Guid referralUid, CancellationToken cancellationToken = default)
    {
        var referral = await referrals.GetByUidAsync(patientUid, referralUid, cancellationToken);
        if (referral is null) return null;
        if (referral.Status != ReferralStatus.Draft) throw new PatientReferralTransitionException("Only a Draft referral can be previewed.");
        return (await BuildArtifactAsync(referral, (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime, cancellationToken)).PdfContent;
    }

    public async Task<ReferralArtifactDownload?> OpenArtifactAsync(Guid patientUid, Guid referralUid,
        CancellationToken cancellationToken = default)
    {
        var artifact = await referrals.GetArtifactAsync(patientUid, referralUid, cancellationToken);
        return artifact is null ? null : new(new MemoryStream(artifact.PdfContent, writable: false),
            artifact.FileName, artifact.MimeType, artifact.FileSizeBytes);
    }

    public async Task<PatientReferralDetailsResponse?> MarkSentAsync(
        Guid patientUid, Guid referralUid, ReferralStatusTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsurePatientExistsAsync(patientUid, cancellationToken);
        var current = await referrals.GetByUidAsync(patientUid, referralUid, cancellationToken);
        if (current is null) return null;
        transitionService.EnsureCanTransition(current.Status, ReferralStatus.Sent);
        if (!string.Equals(current.RowVersion, request.RowVersion, StringComparison.Ordinal))
            throw new PatientReferralConcurrencyException();
        var actorId = await clinicalUserAccessor.GetRequiredUserIdAsync(cancellationToken);
        if (patientService is null || clinicService is null || documentLinks is null || printLayout is null || pdfRenderer is null)
        {
            var legacy = await referrals.MarkSentAsync(patientUid, referralUid, request.RowVersion, actorId, cancellationToken);
            return legacy is null ? null : MapDetails(legacy);
        }
        var sentAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var artifact = await BuildArtifactAsync(current, sentAt, cancellationToken);
        var updated = await referrals.SendWithArtifactAsync(patientUid, referralUid, request.RowVersion,
            actorId, artifact, cancellationToken);
        return updated is null ? null : MapDetails(updated);
    }

    public Task<PatientReferralDetailsResponse?> MarkResponseReceivedAsync(
        Guid patientUid, Guid referralUid, ReferralStatusTransitionRequest request,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(patientUid, referralUid, request, ReferralStatus.ResponseReceived,
            referrals.MarkResponseReceivedAsync, cancellationToken);

    public Task<PatientReferralDetailsResponse?> CloseAsync(
        Guid patientUid, Guid referralUid, ReferralStatusTransitionRequest request,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(patientUid, referralUid, request, ReferralStatus.Closed,
            referrals.CloseAsync, cancellationToken);

    private async Task<PatientReferralDetailsResponse?> TransitionAsync(
        Guid patientUid,
        Guid referralUid,
        ReferralStatusTransitionRequest request,
        ReferralStatus targetStatus,
        Func<Guid, Guid, string, long, CancellationToken, Task<PatientReferral?>> persist,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RowVersion))
            throw new ArgumentException("RowVersion is required.", nameof(request));

        await EnsurePatientExistsAsync(patientUid, cancellationToken);
        var current = await referrals.GetByUidAsync(patientUid, referralUid, cancellationToken);
        if (current is null) return null;

        transitionService.EnsureCanTransition(current.Status, targetStatus);
        var actorId = await clinicalUserAccessor.GetRequiredUserIdAsync(cancellationToken);
        var updated = await persist(
            patientUid, referralUid, request.RowVersion, actorId, cancellationToken);
        return updated is null ? null : MapDetails(updated);
    }

    private async Task EnsurePatientExistsAsync(Guid patientUid, CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty ||
            await patients.GetByUidAsync(patientUid, cancellationToken) is null)
        {
            throw new PatientReferralPatientNotFoundException();
        }
    }

    private static void Validate(CreatePatientReferralRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RecipientName))
            throw new ArgumentException("Recipient name is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Referral reason is required.", nameof(request));
        if (request.RecipientName.Length > 200)
            throw new ArgumentException("Recipient name cannot exceed 200 characters.", nameof(request));
        if (request.RecipientOrganization?.Length > 200)
            throw new ArgumentException("Recipient organization cannot exceed 200 characters.", nameof(request));
        if (request.RecipientPhone?.Length > 30)
            throw new ArgumentException("Recipient phone cannot exceed 30 characters.", nameof(request));
        if (request.RecipientFax?.Length > 30)
            throw new ArgumentException("Recipient fax cannot exceed 30 characters.", nameof(request));
        if (request.Reason.Length > 1000)
            throw new ArgumentException("Referral reason cannot exceed 1000 characters.", nameof(request));
    }

    private static void Validate(UpdatePatientReferralDraftRequest request)
    {
        Validate(new CreatePatientReferralRequest { ReferringProviderUid=request.ReferringProviderUid,
            RecipientName=request.RecipientName,RecipientOrganization=request.RecipientOrganization,
            RecipientPhone=request.RecipientPhone,RecipientFax=request.RecipientFax,Reason=request.Reason,
            ClinicalSummary=request.ClinicalSummary });
        if (string.IsNullOrWhiteSpace(request.RowVersion)) throw new ArgumentException("RowVersion is required.", nameof(request));
    }

    private async Task<ReferralArtifactWrite> BuildArtifactAsync(PatientReferral referral, DateTime sentAt,
        CancellationToken cancellationToken)
    {
        var patient = await (patientService ?? throw new InvalidOperationException("Referral letter patient service is unavailable.")).GetByUidAsync(referral.PatientUid, cancellationToken)
            ?? throw new PatientReferralPatientNotFoundException();
        var provider = referral.ReferringProviderUid.HasValue
            ? await referrals.GetProviderAsync(referral.ReferringProviderUid.Value, cancellationToken) : null;
        if (provider is null) throw new ArgumentException("The referring provider is unavailable.");
        var clinic = await (clinicService ?? throw new InvalidOperationException("Referral letter clinic service is unavailable.")).GetAsync(cancellationToken);
        var documents = await (documentLinks ?? throw new InvalidOperationException("Referral letter document service is unavailable.")).GetByReferralUidAsync(referral.PatientUid, referral.ReferralUid, cancellationToken);
        var credential = string.Join(" | ", new[] { provider.ProviderType, provider.Specialty, provider.BillingNumber }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var snapshot = new
        {
            referral.ReferralUid, referral.PatientUid, PatientName=patient.FullName, patient.DateOfBirth,
            patient.HealthCardNumber, patient.HealthCardVersion, patient.ChartNumber,
            ClinicName=string.IsNullOrWhiteSpace(clinic.LegalName)?clinic.ClinicName:clinic.LegalName,
            clinic.AddressLine1,clinic.AddressLine2,clinic.City,clinic.ProvinceState,clinic.PostalCode,clinic.Phone,clinic.Fax,clinic.Email,
            provider.ProviderUid,ProviderName=provider.DisplayName,ProviderCredential=credential,
            referral.RecipientName,referral.RecipientOrganization,referral.RecipientPhone,referral.RecipientFax,
            referral.Reason,referral.ClinicalSummary,SentAtUtc=sentAt,
            SupportingDocuments=documents.Select(x=>new{x.DocumentUid,x.Title,x.DocumentType,x.DocumentStatus}).ToArray()
        };
        var body = $"<section><h1>Referral Letter</h1><h2>To</h2><p><strong>{E(referral.RecipientName)}</strong><br>{E(referral.RecipientOrganization)}<br>{E(referral.RecipientPhone)} {E(referral.RecipientFax)}</p><h2>Reason for referral</h2><p>{E(referral.Reason)}</p><h2>Clinical summary</h2><p>{E(referral.ClinicalSummary)}</p>{SupportingHtml(documents)}</section>";
        var context = new ClinicalPrintContext(
            new(string.IsNullOrWhiteSpace(clinic.LegalName)?clinic.ClinicName:clinic.LegalName!,clinic.AddressLine1,clinic.AddressLine2,clinic.City,clinic.ProvinceState,clinic.PostalCode,clinic.Phone,clinic.Fax,clinic.Email),
            new(patient.FullName,patient.DateOfBirth,patient.HealthCardNumber,patient.HealthCardVersion,patient.ChartNumber),
            new("Referral","Referral Letter","Outgoing referral",sentAt,provider.DisplayName),
            new("Referring provider",provider.DisplayName,sentAt,null,null),clinic.TimeZoneId);
        var bytes = await (pdfRenderer ?? throw new InvalidOperationException("Referral letter PDF renderer is unavailable."))
            .RenderAsync((printLayout ?? throw new InvalidOperationException("Referral letter print layout is unavailable.")).Render(context,body),cancellationToken);
        return new(Guid.NewGuid(),sentAt,bytes,$"referral-{referral.ReferralUid:N}.pdf",
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),JsonSerializer.Serialize(snapshot),
            provider.DisplayName,string.IsNullOrWhiteSpace(credential)?null:credential);
    }

    private static string SupportingHtml(IReadOnlyList<ReferralDocumentLinkResponse> documents) => documents.Count == 0
        ? string.Empty : "<h2>Supporting documents</h2><ul>"+string.Concat(documents.Select(x=>$"<li>{E(x.Title)} ({E(x.DocumentType)})</li>"))+"</ul>";
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PatientReferralListItemResponse MapListItem(PatientReferral referral) => new()
    {
        ReferralUid = referral.ReferralUid,
        PatientUid = referral.PatientUid,
        RecipientName = referral.RecipientName,
        RecipientOrganization = referral.RecipientOrganization,
        Reason = referral.Reason,
        Status = referral.Status.ToString(),
        CreatedAtUtc = referral.CreatedAt,
        SentAtUtc = referral.SentAt,
        ResponseReceivedAtUtc = referral.ResponseReceivedAt,
        ClosedAtUtc = referral.ClosedAt,
        RowVersion = referral.RowVersion
        ,ReferringProviderUid=referral.ReferringProviderUid,ReferringProviderDisplayName=referral.ReferringProviderDisplayNameSnapshot,
        ArtifactUid=referral.ArtifactUid
    };

    private static PatientReferralDetailsResponse MapDetails(PatientReferral referral) => new()
    {
        ReferralUid = referral.ReferralUid,
        PatientUid = referral.PatientUid,
        RecipientName = referral.RecipientName,
        RecipientOrganization = referral.RecipientOrganization,
        RecipientPhone = referral.RecipientPhone,
        RecipientFax = referral.RecipientFax,
        Reason = referral.Reason,
        ClinicalSummary = referral.ClinicalSummary,
        Status = referral.Status.ToString(),
        CreatedAtUtc = referral.CreatedAt,
        CreatedBy = referral.CreatedBy,
        UpdatedAtUtc = referral.UpdatedAt,
        UpdatedBy = referral.UpdatedBy,
        SentAtUtc = referral.SentAt,
        ResponseReceivedAtUtc = referral.ResponseReceivedAt,
        ClosedAtUtc = referral.ClosedAt,
        RowVersion = referral.RowVersion
        ,ReferringProviderUid=referral.ReferringProviderUid,ReferringProviderDisplayName=referral.ReferringProviderDisplayNameSnapshot,
        ReferringProviderCredential=referral.ReferringProviderCredentialSnapshot,ArtifactUid=referral.ArtifactUid
    };
}

public sealed class PatientReferralPatientNotFoundException()
    : Exception("The requested patient was not found.");
