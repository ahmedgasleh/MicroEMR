using System.Globalization;
using System.Text.Json;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Application.PatientResults;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Application.Templates.Runtime;
using MicroEMR.Application.Tenancy;

namespace MicroEMR.Application.PatientEncounters.Chronology;

public sealed class EncounterChronologyService(IPatientRepository patients, IPatientEncounterService encounters,
    IEncounterDiagnosisRepository diagnoses, IPatientDocumentService documents, IPatientFileRepository files,
    IPatientPrescriptionRepository prescriptions, IPatientResultRepository results, IPatientReferralRepository referrals,
    IClinicalOutputArtifactRepository artifacts, IPatientFileStorage storage, IClinicalArtifactService artifactService,
    ITemplateInstanceRuntime runtime, ICurrentUserPermissionService permissions,
    IPatientChartReadAuditService chartAudit, IStructuredReadAuditService readAudit, ITenantContext tenant) : IEncounterChronologyService
{
    public async Task<EncounterChronologyResponse?> GetAsync(Guid patientUid, EncounterChronologyRequest request,
        string correlation, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (patientUid == Guid.Empty || request.EndDate < request.StartDate
            || request.Direction is not ("Ascending" or "Descending"))
            throw new ArgumentException("Select a patient, valid date range and chronological direction.");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new ArgumentException("The chronological display time zone is invalid."); }
        var access = await Access(token);
        var patient = await patients.GetByUidAsync(patientUid, token);
        if (patient is null) return null;
        Owned(patientUid, patient.PatientUid);
        await chartAudit.RecordOpenedAsync(patientUid, correlation, token);
        var entries = new Dictionary<(string, Guid), EncounterChronologyEntry>();
        var qualifications = new List<string>();
        DateOnly Date(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));
        bool Included(DateOnly date) => (!request.StartDate.HasValue || date >= request.StartDate) && (!request.EndDate.HasValue || date <= request.EndDate);
        void Add(EncounterChronologyEntry entry) { if (Included(entry.ClinicalDate)) entries.TryAdd((entry.SourceType, entry.SourceUid), entry); }

        foreach (var item in (await encounters.GetByPatientUidAsync(patientUid, token)).DistinctBy(x => x.EncounterUid))
        {
            Owned(patientUid, item.PatientUid);
            var addendums = await encounters.GetAddendumsAsync(patientUid, item.EncounterUid, token);
            foreach (var addendum in addendums)
            {
                Owned(patientUid, addendum.PatientUid); Identity(item.EncounterUid, addendum.EncounterUid);
            }
            var includeNote = Included(Date(item.EncounterDateUtc));
            if (!includeNote && !addendums.Any(x => Included(Date(x.CreatedAt)))) continue;
            var encounter = await encounters.GetByUidAsync(item.EncounterUid, token) ?? throw Unavailable();
            Owned(patientUid, encounter.PatientUid); Identity(item.EncounterUid, encounter.EncounterUid);
            var note = string.Join("\n\n", new[] {
                Text("Reason", encounter.ReasonForVisit), Text("Provider", encounter.ProviderName), Text("Notes", encounter.Notes),
                Text("Subjective", encounter.SubjectiveNote), Text("Objective", encounter.ObjectiveNote),
                Text("Assessment", encounter.AssessmentNote), Text("Plan", encounter.PlanNote),
                encounter.Status == "Signed" ? "" : Structured(encounter.TemplateDefinition, encounter.StructuredDataJson)
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
            var diagnosisSet = await diagnoses.GetAsync(patientUid, item.EncounterUid, token);
            if (diagnosisSet is not null)
            {
                Owned(patientUid, diagnosisSet.PatientUid); Identity(item.EncounterUid, diagnosisSet.EncounterUid);
                note += string.Concat(diagnosisSet.Diagnoses.Select(x => "\n\nDiagnosis: " + x.Name + "\n" + x.Description
                    + (x.OnsetDate.HasValue ? "\nOnset: " + x.OnsetDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "")));
            }
            await Audit(ReadAuditActions.EncounterViewed, ReadAuditResourceTypes.Encounter, item.EncounterUid);
            ChronologyAttachment? attachment = null;
            if (encounter.Status == "Signed" && encounter.TemplateVersionUid.HasValue)
                attachment = await FinalAttachment(ClinicalArtifactTypes.Encounter, item.EncounterUid, "EncounterPdf");
            if (includeNote) Add(new("Encounter", item.EncounterUid, Date(encounter.EncounterDateUtc), encounter.EncounterDateUtc,
                encounter.EncounterType, encounter.Status, item.EncounterUid, note, attachment,
                attachment is { Available: false } ? "Preserved encounter PDF unavailable." : null));
            foreach (var addendum in addendums)
            {
                Owned(patientUid, addendum.PatientUid); Identity(item.EncounterUid, addendum.EncounterUid);
                Add(new("Addendum", addendum.EncounterAddendumUid, Date(addendum.CreatedAt), addendum.CreatedAt,
                    "Encounter addendum", "Recorded", item.EncounterUid,
                    Text("Author", addendum.CreatedByDisplayName) + "\n\n" + addendum.AddendumText));
            }
        }

        foreach (var prescription in (await prescriptions.ListAsync(patientUid, token)).DistinctBy(x => x.PrescriptionUid))
        {
            Owned(patientUid, prescription.PatientUid);
            if (!Included(prescription.PrescribedDate)) continue;
            string content; ChronologyAttachment? attachment = null; string? warning = null;
            if (prescription.Status != PrescriptionStatuses.Draft)
            {
                var snapshot = await prescriptions.GetArtifactAsync(patientUid, prescription.PrescriptionUid, token);
                if (snapshot is null) { content = ""; warning = "Preserved prescription snapshot unavailable."; }
                else
                {
                    using var json = JsonDocument.Parse(snapshot.Json);
                    // Final history is rendered from the immutable snapshot, never regenerated from mutable fields.
                    if (snapshot.ArtifactUid != prescription.ArtifactUid
                        || (json.RootElement.TryGetProperty("PatientUid", out var owner) && owner.GetGuid() != patientUid)
                        || !json.RootElement.TryGetProperty("PrescriptionUid", out var source) || source.GetGuid() != prescription.PrescriptionUid)
                        throw new UnauthorizedAccessException("Prescription context is unavailable.");
                    content = string.Join("\n", json.RootElement.EnumerateObject().Select(x => x.Name + ": " + x.Value));
                    attachment = new("PrescriptionPdf", prescription.PrescriptionUid, snapshot.ArtifactUid,
                        "Prescription artifact " + snapshot.ArtifactUid, true);
                }
            }
            else content = string.Join("\n", new[] { "Draft — not finalized", prescription.ProductDisplayText,
                Text("Directions", prescription.Directions), Text("Route / frequency", prescription.Route + " / " + prescription.FrequencyDisplay),
                Text("Quantity / repeats", $"{prescription.Quantity} {prescription.QuantityUnit} / {prescription.AuthorizedRepeats}"), Text("Indication", prescription.Indication) });
            Add(new("Prescription", prescription.PrescriptionUid, prescription.PrescribedDate, null,
                "Prescription history", prescription.Status, null, content, attachment, warning));
        }

        if (access.Contains(PermissionKeys.DocumentsView))
        {
            foreach (var item in (await documents.GetByPatientUidAsync(patientUid, token)).DistinctBy(x => x.DocumentUid))
            {
                Owned(patientUid, item.PatientUid);
                // PatientDocument has no clinical-date/encounter column: use its recorded creation timestamp explicitly.
                if (!Included(Date(item.CreatedAt))) continue;
                var document = await documents.GetByUidAsync(item.DocumentUid, token) ?? throw Unavailable();
                Owned(patientUid, document.PatientUid); Identity(item.DocumentUid, document.DocumentUid);
                await Audit(ReadAuditActions.PatientDocumentViewed, ReadAuditResourceTypes.PatientDocument, item.DocumentUid);
                var attachment = document.IsConsultationReport && document.Status == "Signed"
                    ? await FinalAttachment(ClinicalArtifactTypes.PatientDocument, item.DocumentUid, "DocumentPdf") : null;
                var content = attachment is null ? (document.StructuredDataJson is null ? document.Content
                    : Structured(document.TemplateDefinition, document.StructuredDataJson)) : "Preserved signed consultation report: see printable attachment.";
                Add(new("PatientDocument", item.DocumentUid, Date(document.CreatedAt), document.CreatedAt,
                    document.Title + " (" + document.DocumentType + ")", document.Status, null, content, attachment,
                    attachment is { Available: false } ? "Preserved consultation PDF unavailable."
                        : string.IsNullOrWhiteSpace(content) ? "Document content unavailable." : null));
            }
            foreach (var file in (await files.GetByPatientUidAsync(patientUid, token)).DistinctBy(x => x.FileUid))
            {
                Owned(patientUid, file.PatientUid);
                var date = file.DocumentDate ?? file.ReceivedDate ?? Date(file.UploadedAtUtc);
                if (!Included(date)) continue;
                // Older metadata can lack the current storage context. Never probe or link that path;
                // retain the authorized record as unavailable rather than rejecting readable notes.
                var validStorage = tenant.TenantUid != Guid.Empty
                    && file.StorageKey == $"tenants/{tenant.TenantUid:N}/" + PatientFileNaming.StorageKey(patientUid, file.FileUid);
                var exists = validStorage && await storage.ExistsAsync(file.StorageKey, token);
                var printable = file.ContentType is "application/pdf" or "image/png" or "image/jpeg" or "text/plain";
                var available = exists && printable;
                Add(new("PatientFile", file.FileUid, date, file.DocumentDate.HasValue || file.ReceivedDate.HasValue ? null : file.UploadedAtUtc,
                    file.Title ?? file.OriginalFileName, file.Status.ToString(), null,
                    Text("Category", file.Category) + "\n" + Text("Source", file.SourceOrganization) + "\n" + Text("Description", file.Description),
                    new("PatientFile", file.FileUid, null, $"Patient file {file.FileUid}; {file.OriginalFileName}; SHA-256 {file.Sha256Hash ?? "not recorded"}", available),
                    !validStorage ? "File storage ownership could not be verified. Attachment unavailable."
                        : !exists ? "File content unavailable." : !printable ? "File format has no supported printable representation." : null));
            }
        }
        else qualifications.Add("Documents and scanned files restricted: Documents.View is required. This output is incomplete.");

        if (access.Contains(PermissionKeys.ResultsView))
            foreach (var result in (await results.ListChronologyAsync(patientUid, token)).DistinctBy(x => x.PatientResultUid))
            {
                Owned(patientUid, result.PatientUid);
                Add(new("Result", result.PatientResultUid, Date(result.ResultDate), result.ResultDate,
                    result.ResultName + " (" + result.ResultType + ")", result.ResultStatus + " / " + result.LifecycleStatus, null,
                    string.Join("\n", new[] { Text("Result", result.ResultValue + " " + result.ResultUnit), Text("Reference range", result.ReferenceRange),
                        Text("Summary", result.ResultSummary), Text("Abnormality", result.Abnormality), Text("Source", result.SourceOrganization),
                        Text("Review", result.ReviewNote), Text("Entered in error reason", result.EnteredInErrorReason) })));
            }
        else qualifications.Add("Results restricted: Results.View is required. This output is incomplete.");

        if (access.Contains(PermissionKeys.ReferralsView))
            foreach (var referral in (await referrals.GetByPatientUidAsync(patientUid, token)).DistinctBy(x => x.ReferralUid))
            {
                Owned(patientUid, referral.PatientUid);
                var date = referral.SentAt ?? referral.CreatedAt;
                if (!Included(Date(date))) continue;
                var artifact = await referrals.GetArtifactAsync(patientUid, referral.ReferralUid, token);
                var expected = referral.ArtifactUid.HasValue || referral.SentAt.HasValue;
                if (artifact is not null && artifact.ArtifactUid != referral.ArtifactUid) throw Unavailable();
                var available = artifact is { MimeType: "application/pdf" } && artifact.PdfContent.Length > 0;
                Add(new("Referral", referral.ReferralUid, Date(date), date, "Referral to " + referral.RecipientName,
                    referral.Status.ToString(), null, expected ? "Preserved sent referral letter: see printable attachment."
                        : Text("Reason", referral.Reason) + "\n\n" + Text("Clinical summary", referral.ClinicalSummary),
                    expected ? new("ReferralPdf", referral.ReferralUid, referral.ArtifactUid, "Referral artifact " + referral.ArtifactUid, available) : null,
                    expected && !available ? "Preserved referral letter unavailable." : null));
            }
        else qualifications.Add("Referrals restricted: Referrals.View is required. This output is incomplete.");

        var ordered = entries.Values.OrderBy(x => x.ClinicalDate).ThenBy(x => x.ClinicalDateTimeUtc ?? DateTime.MinValue)
            .ThenBy(x => x.SourceType, StringComparer.Ordinal).ThenBy(x => x.SourceUid);
        var output = request.Direction == "Descending" ? ordered.Reverse().ToArray() : ordered.ToArray();
        return new(patientUid, patient.LastName + ", " + patient.FirstName, patient.ChartNumber, patient.HealthCardNumber,
            patient.DateOfBirth, request, output, qualifications, DateTime.UtcNow);

        Task<Guid> Audit(string action, string type, Guid uid) => readAudit.RecordAsync(action, type, uid, patientUid, correlation, token);
        async Task<ChronologyAttachment> FinalAttachment(string type, Guid uid, string kind)
        {
            var artifact = await artifacts.GetFinalBySourceAsync(type, uid, token);
            if (artifact is not null) { Owned(patientUid, artifact.PatientUid); Identity(uid, artifact.SourceUid); if (artifact.SourceType != type) throw Unavailable(); }
            var folder = type == ClinicalArtifactTypes.Encounter ? "encounters" : "patient-documents";
            var validStorage = artifact is not null && tenant.TenantUid != Guid.Empty && artifact.StorageKey.StartsWith(
                $"tenants/{tenant.TenantUid:N}/clinical-artifacts/{folder}/{patientUid:N}/{uid:N}/", StringComparison.Ordinal);
            var available = validStorage && artifact is { MimeType: "application/pdf", Status: "Available", ArtifactType: "FinalPdf" }
                && await storage.ExistsAsync(artifact.StorageKey, token);
            return new(kind, uid, artifact?.ArtifactUid, $"{type} {uid}; final artifact {artifact?.ArtifactUid}; SHA-256 {artifact?.Sha256 ?? "unavailable"}", available);
        }
    }

    public async Task<ClinicalArtifactContent?> OpenEncounterPdfAsync(Guid patientUid, Guid encounterUid, string correlation, CancellationToken token = default)
    {
        await Access(token);
        var encounter = await encounters.GetByUidAsync(encounterUid, token);
        if (encounter is null) return null;
        Owned(patientUid, encounter.PatientUid); Identity(encounterUid, encounter.EncounterUid);
        if (encounter.Status != "Signed") return null;
        var artifact = await artifacts.GetFinalBySourceAsync(ClinicalArtifactTypes.Encounter, encounterUid, token);
        if (artifact is null) return null;
        Owned(patientUid, artifact.PatientUid); Identity(encounterUid, artifact.SourceUid);
        if (artifact.SourceType != ClinicalArtifactTypes.Encounter || tenant.TenantUid == Guid.Empty
            || !artifact.StorageKey.StartsWith($"tenants/{tenant.TenantUid:N}/clinical-artifacts/encounters/{patientUid:N}/{encounterUid:N}/", StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Encounter artifact context is unavailable.");
        var content = await artifactService.OpenEncounterFinalPdfAsync(encounterUid, token);
        if (content is null) return null;
        try { await readAudit.RecordAsync(ReadAuditActions.EncounterViewed, ReadAuditResourceTypes.Encounter, encounterUid, patientUid, correlation, token); }
        catch { await content.Content.DisposeAsync(); throw; }
        return content;
    }

    private async Task<IReadOnlySet<string>> Access(CancellationToken token)
    {
        var access = await permissions.GetEffectivePermissionsAsync(token);
        if (!access.Contains(PermissionKeys.PatientsView) || !access.Contains(PermissionKeys.EncountersView))
            throw new UnauthorizedAccessException("Chronological content access is restricted.");
        return access;
    }
    private string Structured(TemplateDefinition? definition, string? json)
    {
        if (json is null) return "";
        if (definition is null) throw Unavailable();
        var value = runtime.Process(definition, json);
        if (!value.IsValid || value.Data is null) throw Unavailable();
        return runtime.RenderSnapshot(definition, value.Data);
    }
    private static string Text(string label, string? value) => string.IsNullOrWhiteSpace(value) ? "" : label + ": " + value;
    private static void Owned(Guid requested, Guid actual) { if (requested != actual) throw new UnauthorizedAccessException("Clinical source context is unavailable."); }
    private static void Identity(Guid requested, Guid actual) { if (requested != actual) throw Unavailable(); }
    private static IOException Unavailable() => new("Required chronological source content is unavailable. No complete output was returned.");
}
