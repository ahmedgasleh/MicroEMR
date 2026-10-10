using System.Reflection;
using System.Text.Json;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.PatientEncounters.Chronology;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Application.PatientResults;
using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.Templates.Runtime;
using MicroEMR.Application.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterChronologyTests
{
    [Fact]
    public async Task AllSupportedCategoriesHaveContentOrActualPrintableIdentityWithoutInferredEncounterLinks()
    {
        var f = new Fixture(); f.Populate();
        var response = (await f.Read())!;
        Assert.True(response.IsComplete); Assert.Equal(8, response.Entries.Count);
        foreach (var category in new[] { "Encounter", "Prescription", "Result", "PatientFile", "Referral" })
            Assert.Contains(response.Entries, x => x.SourceType == category);
        foreach (var category in new[] { "Report", "Requisition", "Letter" })
            Assert.Contains(response.Entries, x => x.Title.Contains(category) && x.Content.Contains("Actual content"));
        Assert.Contains(response.Entries, x => x.Content.Contains("Actual SOAP assessment"));
        Assert.Contains(response.Entries, x => x.Content.Contains("Historical prescribed product"));
        Assert.All(response.Entries.Where(x => x.SourceType != "Encounter"), x => Assert.Null(x.EncounterUid));
        Assert.All(response.Entries.Where(x => x.Attachment is not null), x => Assert.True(x.Attachment!.Available));
        Assert.Contains(f.Audits, x => x.Action == ReadAuditActions.EncounterViewed);
        Assert.Equal(3, f.Audits.Count(x => x.Action == ReadAuditActions.PatientDocumentViewed));
        Assert.DoesNotContain(f.Audits, x => x.Action.EndsWith("Downloaded")); // Manifest retrieval is not a download.
    }

    [Theory]
    [InlineData("Ascending")][InlineData("Descending")]
    public async Task DirectionIsDeterministicAndDateRangeIncludesBothBoundaryDates(string direction)
    {
        var f = new Fixture(); f.Populate();
        var response = (await f.Read(new() { StartDate = new(2030, 1, 2), EndDate = new(2030, 1, 4), Direction = direction }))!;
        Assert.All(response.Entries, x => Assert.InRange(x.ClinicalDate, new(2030, 1, 2), new(2030, 1, 4)));
        Assert.Contains(response.Entries, x => x.ClinicalDate == new DateOnly(2030, 1, 2));
        Assert.Contains(response.Entries, x => x.ClinicalDate == new DateOnly(2030, 1, 4));
        var dates = response.Entries.Select(x => x.ClinicalDate).ToArray();
        Assert.Equal(direction == "Ascending" ? dates.Order().ToArray() : dates.OrderDescending().ToArray(), dates);
        Assert.Equal(response.Entries.Select(x => x.SourceUid), (await f.Read(response.Criteria))!.Entries.Select(x => x.SourceUid));
    }

    [Fact]
    public async Task LocalMidnightAndDateOnlyRecordsRetainCalendarDates()
    {
        var f = new Fixture(); f.Populate(); f.Encounters[0].EncounterDateUtc = new(2030, 1, 2, 1, 0, 0, DateTimeKind.Utc);
        var response = (await f.Read(new() { StartDate = new(2030, 1, 1), EndDate = new(2030, 1, 1), TimeZoneId = "America/Toronto" }))!;
        Assert.Single(response.Entries); Assert.Equal("Encounter", response.Entries[0].SourceType);
        Assert.Equal(new DateOnly(2030, 1, 1), response.Entries[0].ClinicalDate);
    }

    [Fact]
    public async Task AddendumInRangeSurvivesWhenParentEncounterIsOutsideRange()
    {
        var f = new Fixture(); f.Populate(); var parent = f.Encounters[0];
        f.Addendums.Add(new() { EncounterAddendumUid = Guid.NewGuid(), EncounterUid = parent.EncounterUid,
            PatientUid = f.Patient, CreatedAt = At(8), AddendumText = "Later addendum" });
        var response = (await f.Read(new() { StartDate = new(2030, 1, 8), EndDate = new(2030, 1, 8) }))!;
        Assert.Single(response.Entries); Assert.Equal("Later addendum", response.Entries[0].Content.Trim());
        Assert.Equal(parent.EncounterUid, response.Entries[0].EncounterUid);
    }

    [Fact]
    public async Task DuplicateSourceRowsDoNotDuplicateArtifactEntries()
    {
        var f = new Fixture(); f.Populate(); f.Encounters.Add(f.Encounters[0]); f.Documents.Add(f.Documents[0]); f.Files.Add(f.Files[0]);
        var response = (await f.Read())!; Assert.Equal(8, response.Entries.Count);
        Assert.Equal(response.Entries.Count, response.Entries.DistinctBy(x => (x.SourceType, x.SourceUid)).Count());
    }

    [Theory]
    [InlineData("Patients.View")][InlineData("Encounters.View")]
    public async Task MissingBasePermissionRejectsBeforePatientOrSourceAccess(string permission)
    {
        var f = new Fixture(); f.Access.Remove(permission);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Read()); Assert.Equal(0, f.PatientReads);
    }

    [Theory]
    [InlineData("Documents.View")][InlineData("Results.View")][InlineData("Referrals.View")]
    public async Task RestrictedSourcesAreNotQueriedOrSilentlyClaimedComplete(string permission)
    {
        var f = new Fixture(); f.Populate(); f.Access.Remove(permission);
        var response = (await f.Read())!; Assert.False(response.IsComplete);
        Assert.Contains(response.Qualifications, x => x.Contains(permission));
        Assert.DoesNotContain(response.Entries, x => permission switch {
            "Documents.View" => x.SourceType is "PatientDocument" or "PatientFile",
            "Results.View" => x.SourceType == "Result", _ => x.SourceType == "Referral" });
    }

    [Theory]
    [InlineData("Encounter")][InlineData("Document")][InlineData("Prescription")][InlineData("Result")][InlineData("Patient")]
    public async Task ForeignPatientContextCannotDiscloseClinicalContent(string kind)
    {
        var f = new Fixture(); f.Populate(); var foreign = Guid.NewGuid();
        if (kind == "Encounter") f.Encounters[0].PatientUid = foreign;
        if (kind == "Document") f.Documents[0].PatientUid = foreign;
        if (kind == "Prescription") f.Prescriptions[0].PatientUid = foreign;
        if (kind == "Result") f.Results[0].PatientUid = foreign;
        if (kind == "Patient") f.PatientOwner = foreign;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Read());
    }

    [Fact]
    public async Task MissingPhysicalFileIsExplicitAndHasNoAvailableDownloadReference()
    {
        var f = new Fixture(); f.Populate(); f.FileExists = false;
        var response = (await f.Read())!; Assert.False(response.IsComplete);
        var file = Assert.Single(response.Entries, x => x.SourceType == "PatientFile");
        Assert.False(file.Attachment!.Available); Assert.Equal("File content unavailable.", file.Warning);
    }

    [Fact]
    public async Task SignedSourcesUsePreservedPdfIdentityAndNeverCreateOrModifyArtifacts()
    {
        var f = new Fixture(); f.Populate(); var encounter = f.Encounters[0];
        encounter.Status = "Signed"; encounter.TemplateVersionUid = Guid.NewGuid(); encounter.StructuredDataJson = "historical data";
        var document = f.Documents[0]; document.Status = "Signed"; document.IsConsultationReport = true;
        document.StructuredDataJson = "historical document";
        var response = (await f.Read())!;
        Assert.Contains(response.Entries, x => x.SourceType == "Encounter" && x.Attachment!.ArtifactUid == f.Artifact);
        Assert.Contains(response.Entries, x => x.SourceType == "PatientDocument" && x.Attachment is { Kind: "DocumentPdf", Available: true });
        Assert.Equal("historical data", encounter.StructuredDataJson); Assert.Equal("historical document", document.StructuredDataJson);
        Assert.Contains("Actual SOAP assessment", response.Entries.Single(x => x.SourceType == "Encounter").Content);
    }

    [Fact]
    public async Task ForeignArtifactOwnerFailsClosed()
    {
        var f = new Fixture(); f.Populate(); f.Encounters[0].Status = "Signed"; f.Encounters[0].TemplateVersionUid = Guid.NewGuid();
        f.ArtifactOwner = Guid.NewGuid(); await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Read());
    }

    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task AuditFailurePreventsChronologyDisclosure(bool chart)
    {
        var f = new Fixture(); f.Populate(); f.FailChartAudit = chart; f.FailReadAudit = !chart;
        await Assert.ThrowsAsync<IOException>(() => f.Read());
    }

    [Fact]
    public async Task AttachmentRetrievalRechecksPatientOwnershipAndAuditBeforeReturningBytes()
    {
        var f = new Fixture(); f.Populate(); f.Encounters[0].Status = "Signed"; var uid = f.Encounters[0].EncounterUid;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service().OpenEncounterPdfAsync(Guid.NewGuid(), uid, "test"));
        Assert.Equal(0, f.AttachmentOpens);
        f.FailReadAudit = true;
        await Assert.ThrowsAsync<IOException>(() => f.Service().OpenEncounterPdfAsync(f.Patient, uid, "test"));
        Assert.False(f.OpenedStream!.CanRead);
        f.FailReadAudit = false; var content = await f.Service().OpenEncounterPdfAsync(f.Patient, uid, "test");
        Assert.NotNull(content); await content!.Content.DisposeAsync();
    }

    [Theory]
    [InlineData("invalid")][InlineData("Ascending")]
    public async Task InvalidDirectionOrReversedRangeRejects(string direction)
    {
        var f = new Fixture(); await Assert.ThrowsAsync<ArgumentException>(() => f.Read(new() {
            Direction = direction, StartDate = new(2030, 1, 4), EndDate = new(2030, 1, 1) }));
    }

    [Fact]
    public async Task ForeignTenantStorageContextCannotAdvertiseOrOpenAttachments()
    {
        var f = new Fixture(); f.Populate(); f.Tenant = Guid.NewGuid();
        var response = (await f.Read())!;
        var file = Assert.Single(response.Entries, x => x.SourceType == "PatientFile");
        Assert.False(response.IsComplete); Assert.False(file.Attachment!.Available);
        Assert.Equal(0, f.StorageProbes);
        Assert.Contains("ownership could not be verified", file.Warning);
    }

    [Fact]
    public async Task LegacyFilePathDoesNotBlockNotesOrProbeUnverifiedContent()
    {
        var f = new Fixture(); f.Populate(); var file = f.Files[0];
        f.Files[0] = new PatientFile { PatientUid = f.Patient, FileUid = file.FileUid,
            OriginalFileName = file.OriginalFileName, StorageKey = "legacy/scanned-document.pdf",
            ContentType = "application/pdf", UploadedAtUtc = file.UploadedAtUtc, RowVersion = file.RowVersion };
        var response = (await f.Read())!;
        Assert.False(response.IsComplete);
        Assert.Contains(response.Entries, x => x.SourceType == "Encounter" && x.Content.Contains("Actual SOAP assessment"));
        Assert.Contains(response.Entries, x => x.SourceType == "PatientFile" && x.Attachment is { Available: false } && x.Warning is not null);
        Assert.Equal(0, f.StorageProbes);
    }

    [Theory]
    [InlineData("legacy/encounter.pdf")]
    [InlineData("tenants/foreign/clinical-artifacts/encounters/foreign.pdf")]
    public async Task UnverifiedFinalPdfPathDoesNotBlockNotesAndDirectOpenStillRejects(string storageKey)
    {
        var f = new Fixture(); f.Populate(); f.Files.Clear();
        f.Encounters[0].Status = "Signed"; f.Encounters[0].TemplateVersionUid = Guid.NewGuid();
        f.ArtifactStorageKey = storageKey;
        var response = (await f.Read())!;
        var note = Assert.Single(response.Entries, x => x.SourceType == "Encounter");
        Assert.False(response.IsComplete); Assert.False(note.Attachment!.Available); Assert.NotNull(note.Warning);
        Assert.Contains("Actual SOAP assessment", note.Content); Assert.Equal(0, f.StorageProbes);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service().OpenEncounterPdfAsync(f.Patient, note.SourceUid, "test"));
        Assert.Equal(0, f.AttachmentOpens);
    }

    [Fact]
    public async Task CorrectedAndEnteredInErrorResultsRemainVisibleAsHistoricalRecords()
    {
        var f = new Fixture(); f.Populate();
        f.Results.Add(new() { PatientUid = f.Patient, PatientResultUid = Guid.NewGuid(), ResultDate = At(4), ResultName = "Previous report", LifecycleStatus = "Superseded", ResultValue = "41" });
        f.Results.Add(new() { PatientUid = f.Patient, PatientResultUid = Guid.NewGuid(), ResultDate = At(4), ResultName = "Error report", LifecycleStatus = "EnteredInError", EnteredInErrorReason = "Wrong result" });
        var response = (await f.Read())!;
        Assert.Contains(response.Entries, x => x.Status.Contains("Superseded") && x.Content.Contains("41"));
        Assert.Contains(response.Entries, x => x.Status.Contains("EnteredInError") && x.Content.Contains("Wrong result"));
    }

    private static DateTime At(int day) => new(2030, 1, day, 12, 0, 0, DateTimeKind.Utc);
    private sealed class Fixture
    {
        public Guid Patient = Guid.NewGuid(), Artifact = Guid.NewGuid(), Tenant = Guid.NewGuid(); public Guid? PatientOwner, ArtifactOwner;
        public HashSet<string> Access = [PermissionKeys.PatientsView, PermissionKeys.EncountersView, PermissionKeys.DocumentsView, PermissionKeys.ResultsView, PermissionKeys.ReferralsView];
        public List<PatientEncounterDetailsResponse> Encounters = []; public List<PatientEncounterAddendumResponse> Addendums = [];
        public List<PatientDocumentDetailsResponse> Documents = []; public List<PatientPrescriptionResponse> Prescriptions = [];
        public List<PatientResultResponse> Results = []; public List<PatientFile> Files = []; public List<PatientReferral> Referrals = [];
        public List<(string Action, Guid Uid)> Audits = []; public bool FileExists = true, FailChartAudit, FailReadAudit;
        public int StorageProbes; public string? ArtifactStorageKey;
        public int PatientReads, AttachmentOpens; public MemoryStream? OpenedStream;
        public void Populate()
        {
            Encounters.Add(new() { PatientUid = Patient, EncounterUid = Guid.NewGuid(), EncounterDateUtc = At(1), AssessmentNote = "Actual SOAP assessment", Status = "Open", EncounterType = "Progress/SOAP" });
            Prescriptions.Add(new() { PatientUid = Patient, PrescriptionUid = Guid.NewGuid(), ArtifactUid = Artifact, PrescribedDate = new(2030, 1, 2), Status = "Finalized", ProductDisplayText = "Mutable product should not print" });
            foreach (var type in new[] { "Report", "Requisition", "Letter" }) Documents.Add(new() {
                PatientUid = Patient, DocumentUid = Guid.NewGuid(), Title = type, DocumentType = type, CreatedAt = At(3), Content = "Actual content: " + type, Status = "Finalized" });
            Results.Add(new() { PatientUid = Patient, PatientResultUid = Guid.NewGuid(), ResultDate = At(4), ResultName = "Report result", ResultValue = "42" });
            Files.Add(new() { PatientUid = Patient, FileUid = Guid.NewGuid(), Title = "Scanned document", OriginalFileName = "scan.pdf", StorageKey = "test", ContentType = "application/pdf", UploadedAtUtc = At(5), RowVersion = "test" });
            Files[0] = new PatientFile { PatientUid=Patient, FileUid=Files[0].FileUid, OriginalFileName="scan.pdf", Title="Scanned document", StorageKey=$"tenants/{Tenant:N}/"+PatientFileNaming.StorageKey(Patient,Files[0].FileUid), ContentType="application/pdf",UploadedAtUtc=At(5),RowVersion="test" };
            Referrals.Add(new() { PatientUid = Patient, ReferralUid = Guid.NewGuid(), ArtifactUid = Artifact, RecipientName = "Recipient", Reason = "Referral reason", CreatedAt = At(6), SentAt = At(6), Status = ReferralStatus.Sent, RowVersion = "test" });
        }
        public Task<EncounterChronologyResponse?> Read(EncounterChronologyRequest? request = null) => Service().GetAsync(Patient, request ?? new(), "test");
        public EncounterChronologyService Service() => new(
            Proxy<IPatientRepository>((_, _) => { PatientReads++; return Task.FromResult<PatientDetailsResponse?>(new() { PatientUid = PatientOwner ?? Patient, FirstName = "Test", LastName = "Patient" }); }),
            Proxy<IPatientEncounterService>((method, args) => method switch {
                "GetByPatientUidAsync" => Task.FromResult<IReadOnlyList<PatientEncounterListItemResponse>>(Encounters.Select(x => new PatientEncounterListItemResponse { PatientUid = x.PatientUid, EncounterUid = x.EncounterUid, EncounterDateUtc = x.EncounterDateUtc }).ToArray()),
                "GetByUidAsync" => Task.FromResult(Encounters.FirstOrDefault(x => x.EncounterUid == (Guid)args[0]!)),
                "GetAddendumsAsync" => Task.FromResult<IReadOnlyList<PatientEncounterAddendumResponse>>(Addendums.Where(x => x.EncounterUid == (Guid)args[1]!).ToArray()), _ => throw new InvalidOperationException(method) }),
            Proxy<IEncounterDiagnosisRepository>((method, _) => method == "GetAsync" ? Task.FromResult<EncounterDiagnosesResponse?>(null) : throw new InvalidOperationException(method)),
            Proxy<IPatientDocumentService>((method, args) => method switch {
                "GetByPatientUidAsync" => Task.FromResult<IReadOnlyList<PatientDocumentListItemResponse>>(Documents.Select(x => new PatientDocumentListItemResponse { PatientUid = x.PatientUid, DocumentUid = x.DocumentUid, CreatedAt = x.CreatedAt }).ToArray()),
                "GetByUidAsync" => Task.FromResult(Documents.FirstOrDefault(x => x.DocumentUid == (Guid)args[0]!)), _ => throw new InvalidOperationException(method) }),
            Proxy<IPatientFileRepository>((method, _) => method == "GetByPatientUidAsync" ? Task.FromResult<IReadOnlyList<PatientFile>>(Files) : throw new InvalidOperationException(method)),
            Proxy<IPatientPrescriptionRepository>((method, args) => method switch {
                "ListAsync" => Task.FromResult<IReadOnlyList<PatientPrescriptionResponse>>(Prescriptions),
                "GetArtifactAsync" => Task.FromResult<PrescriptionArtifact?>(new(Artifact, JsonSerializer.Serialize(new { PrescriptionUid = (Guid)args[1]!, ProductDisplayText = "Historical prescribed product" }))), _ => throw new InvalidOperationException(method) }),
            Proxy<IPatientResultRepository>((method, _) => method == "ListChronologyAsync" ? Task.FromResult<IReadOnlyList<PatientResultResponse>>(Results) : throw new InvalidOperationException(method)),
            Proxy<IPatientReferralRepository>((method, _) => method switch {
                "GetByPatientUidAsync" => Task.FromResult<IReadOnlyList<PatientReferral>>(Referrals),
                "GetArtifactAsync" => Task.FromResult<ReferralArtifactContent?>(new(Artifact, "application/pdf", "letter.pdf", [1,2], 2, "hash", "{}", At(6))), _ => throw new InvalidOperationException(method) }),
            Proxy<IClinicalOutputArtifactRepository>((method, args) => method == "GetFinalBySourceAsync" ? Task.FromResult<ClinicalOutputArtifact?>(new(Artifact, ArtifactOwner ?? Patient, (string)args[0]!, (Guid)args[1]!, Guid.NewGuid(), "FinalPdf", "FileSystem", ArtifactStorageKey ?? $"tenants/{Tenant:N}/clinical-artifacts/{((string)args[0]! == ClinicalArtifactTypes.Encounter ? "encounters" : "patient-documents")}/{Patient:N}/{(Guid)args[1]!:N}/{Artifact:N}.pdf", "application/pdf", 2, "hash", "Available", 7, At(1))) : throw new InvalidOperationException(method)),
            Proxy<IPatientFileStorage>((method, _) => { if (method != "ExistsAsync") throw new InvalidOperationException(method); StorageProbes++; return Task.FromResult(FileExists); }),
            Proxy<IClinicalArtifactService>((method, _) => { if (method != "OpenEncounterFinalPdfAsync") throw new InvalidOperationException(method); AttachmentOpens++; OpenedStream = new([1,2]); return Task.FromResult<ClinicalArtifactContent?>(new(OpenedStream, "encounter.pdf", "application/pdf", 2)); }),
            Proxy<ITemplateInstanceRuntime>((method, _) => throw new InvalidOperationException(method)),
            Proxy<ICurrentUserPermissionService>((_, _) => Task.FromResult<IReadOnlySet<string>>(Access)),
            Proxy<IPatientChartReadAuditService>((_, _) => FailChartAudit ? throw new IOException("audit unavailable") : Task.FromResult(Guid.NewGuid())),
            Proxy<IStructuredReadAuditService>((_, args) => { if (FailReadAudit) throw new IOException("audit unavailable"); Audits.Add(((string)args[0]!, (Guid)args[2]!)); return Task.FromResult(Guid.NewGuid()); }),
            Proxy<ITenantContext>((_,_)=>Tenant));
    }
    public class Stub : DispatchProxy { public Func<string, object?[], object?> Handler = null!; protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name, args ?? []); }
    private static T Proxy<T>(Func<string, object?[], object?> handler) where T : class { var value = DispatchProxy.Create<T, Stub>(); ((Stub)(object)value).Handler = handler; return value; }
}
