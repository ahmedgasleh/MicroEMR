using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Patients.Repositories;

namespace MicroEMR.Application.PatientReferrals;

public sealed class PatientReferralService(
    IPatientReferralRepository referrals,
    IPatientRepository patients,
    IAuthenticatedClinicalUserAccessor clinicalUserAccessor,
    IReferralStatusTransitionService transitionService) : IPatientReferralService
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

    public Task<PatientReferralDetailsResponse?> MarkSentAsync(
        Guid patientUid, Guid referralUid, ReferralStatusTransitionRequest request,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(patientUid, referralUid, request, ReferralStatus.Sent,
            referrals.MarkSentAsync, cancellationToken);

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
    };
}

public sealed class PatientReferralPatientNotFoundException()
    : Exception("The requested patient was not found.");
