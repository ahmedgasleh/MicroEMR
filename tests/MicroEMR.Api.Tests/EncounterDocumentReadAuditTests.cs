using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.ReadAudit;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class EncounterDocumentReadAuditTests
{
    [Theory]
    [InlineData(ReadAuditActions.EncounterViewed, ReadAuditResourceTypes.Encounter)]
    [InlineData(ReadAuditActions.PatientDocumentViewed, ReadAuditResourceTypes.PatientDocument)]
    public async Task GenericServiceUsesResolvedActorAndNarrowStructuredMetadata(
        string eventType, string resourceType)
    {
        var repository = new RecordingRepository();
        var service = new StructuredReadAuditService(repository, new Actor(91));
        var resourceUid = Guid.NewGuid();
        var patientUid = Guid.NewGuid();

        await service.RecordAsync(eventType, resourceType, resourceUid, patientUid, " trace-15b ");

        Assert.Equal((eventType, resourceType, resourceUid, patientUid, 91L, "trace-15b", "MicroEMR.Api"),
            repository.Recorded);
    }

    [Fact]
    public void DetailEndpointsAuditAfterAuthoritativeResolutionAndFailClosed()
    {
        AssertDetailTrigger("PatientEncountersController.cs", "GetEncounter", "GetByUidAsync",
            "encounter.PatientUid", "encounter.EncounterUid", "EncounterViewed");
        AssertDetailTrigger("PatientDocumentsController.cs", "GetDocument", "GetByUidAsync",
            "document.PatientUid", "document.DocumentUid", "PatientDocumentViewed");
    }

    [Fact]
    public void ListAndAncillaryEndpointsDoNotCreateViewEvents()
    {
        var encounter = ReadController("PatientEncountersController.cs");
        var document = ReadController("PatientDocumentsController.cs");

        Assert.Equal(1, Count(encounter, "ReadAuditActions.EncounterViewed"));
        Assert.Equal(1, Count(document, "ReadAuditActions.PatientDocumentViewed"));
        Assert.DoesNotContain("RecordAsync", Method(encounter, "GetPatientEncounters", "GetEncounter"));
        Assert.DoesNotContain("RecordAsync", Method(document, "GetPatientDocuments", "GetDocument"));
    }

    [Fact]
    public void AuditPayloadContainsNoClinicalContent()
    {
        var audit = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Application", "ReadAudit",
            "PatientChartReadAudit.cs"));
        foreach (var contentName in new[] { "Subjective", "Objective", "Assessment", "PlanNote", "Diagnosis",
                     "AddendumText", "DocumentContent", "StructuredDataJson", "FileBytes" })
            Assert.DoesNotContain(contentName, audit, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertDetailTrigger(string file, string methodName, string resolution,
        string patient, string resource, string eventType)
    {
        var source = ReadController(file);
        var method = Method(source, methodName, "return Ok");
        Assert.True(method.IndexOf(resolution, StringComparison.Ordinal) <
                    method.IndexOf("_readAudit.RecordAsync", StringComparison.Ordinal));
        Assert.Contains(patient, method);
        Assert.Contains(resource, method);
        Assert.Contains(eventType, method);
        Assert.Contains("StatusCodes.Status503ServiceUnavailable", method);
    }

    private static string Method(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        return source[startIndex..(endIndex < 0 ? source.Length : endIndex + end.Length)];
    }

    private static string ReadController(string file) => File.ReadAllText(Path.Combine(
        Root(), "src", "MicroEMR.Api", "Controllers", file));
    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));

    private sealed class Actor(long userId) : IAuthenticatedClinicalUserAccessor
    {
        public Task<long> GetRequiredUserIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(userId);
    }

    private sealed class RecordingRepository : IReadAuditRepository
    {
        public (string Event, string ResourceType, Guid ResourceUid, Guid PatientUid, long Actor,
            string Correlation, string Source) Recorded { get; private set; }

        public Task<Guid> RecordStructuredReadAsync(string eventType, string resourceType, Guid resourceUid,
            Guid patientUid, long clinicalUserId, string requestCorrelationId, string sourceApplication,
            CancellationToken cancellationToken = default)
        {
            Recorded = (eventType, resourceType, resourceUid, patientUid, clinicalUserId,
                requestCorrelationId, sourceApplication);
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<Guid> RecordPatientChartOpenedAsync(Guid patientUid, long clinicalUserId,
            string requestCorrelationId, string sourceApplication,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid> RecordAggregateReportAsync(string eventType, string reportKey, long clinicalUserId,
            string requestCorrelationId, string sourceApplication,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
