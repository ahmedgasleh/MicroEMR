using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Application.ReadAudit;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PatientFileDownloadAuditTests
{
    [Fact]
    public async Task AuthorizedDownloadUsesAuthoritativeResourceAndCreatesExactlyOneEvent()
    {
        var authoritativeFile = Guid.NewGuid();
        var authoritativePatient = Guid.NewGuid();
        var audit = new Audit();
        var controller = Controller(new Files(new PatientFileContent(authoritativeFile, authoritativePatient,
            new MemoryStream([1, 2, 3]), "clinical.pdf", "application/pdf", 3)), audit);

        var result = Assert.IsType<FileStreamResult>(await controller.Content(Guid.NewGuid(), Guid.NewGuid(), default));

        Assert.Equal(3, result.FileStream.Length);
        Assert.Equal(1, audit.Calls);
        Assert.Equal((ReadAuditActions.PatientFileDownloaded, ReadAuditResourceTypes.PatientFile,
            authoritativeFile, authoritativePatient, "step16b1-trace"), audit.Recorded);
    }

    [Fact]
    public async Task RepeatedExplicitDownloadsCreateSeparateEvents()
    {
        var audit = new Audit();
        var files = new Files(() => new PatientFileContent(Guid.NewGuid(), Guid.NewGuid(),
            new MemoryStream([1]), "test.pdf", "application/pdf", 1));
        var controller = Controller(files, audit);

        Assert.IsType<FileStreamResult>(await controller.Content(Guid.NewGuid(), Guid.NewGuid(), default));
        Assert.IsType<FileStreamResult>(await controller.Content(Guid.NewGuid(), Guid.NewGuid(), default));

        Assert.Equal(2, audit.Calls);
    }

    [Fact]
    public async Task MissingOwnershipOrStorageCreatesNoSuccessfulEvent()
    {
        var audit = new Audit();
        var controller = Controller(new Files((PatientFileContent?)null), audit);

        Assert.IsType<NotFoundResult>(await controller.Content(Guid.NewGuid(), Guid.NewGuid(), default));
        Assert.Equal(0, audit.Calls);
    }

    [Fact]
    public async Task AuditFailureDisposesStreamAndPreventsByteResponse()
    {
        var stream = new MemoryStream([1, 2, 3]);
        var audit = new Audit { Failure = new InvalidOperationException("audit unavailable") };
        var controller = Controller(new Files(new PatientFileContent(Guid.NewGuid(), Guid.NewGuid(), stream,
            "test.pdf", "application/pdf", 3)), audit);

        var result = Assert.IsType<ObjectResult>(await controller.Content(Guid.NewGuid(), Guid.NewGuid(), default));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.False(stream.CanRead);
    }

    [Fact]
    public async Task ListAndMetadataRetrievalCreateNoDownloadEvent()
    {
        var audit = new Audit();
        var controller = Controller(new Files((PatientFileContent?)null), audit);

        Assert.IsType<OkObjectResult>((await controller.List(Guid.NewGuid(), default)).Result);
        Assert.IsType<NotFoundResult>((await controller.Get(Guid.NewGuid(), Guid.NewGuid(), default)).Result);
        Assert.Equal(0, audit.Calls);
    }

    [Fact]
    public void AuditContractContainsNoFileContentOrFilename()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Application", "ReadAudit",
            "PatientChartReadAudit.cs"));
        Assert.Contains("PatientFileDownloaded", source);
        Assert.DoesNotContain("FileName", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FileBytes", source, StringComparison.OrdinalIgnoreCase);
    }

    private static PatientFilesController Controller(IPatientFileService files, IStructuredReadAuditService audit)
    {
        var controller = new PatientFilesController(files, audit, NullLogger<PatientFilesController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "step16b1-trace" }
        };
        return controller;
    }

    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));

    private sealed class Audit : IStructuredReadAuditService
    {
        public int Calls { get; private set; }
        public (string Event, string ResourceType, Guid ResourceUid, Guid PatientUid, string Correlation) Recorded { get; private set; }
        public Exception? Failure { get; init; }
        public Task<Guid> RecordAsync(string eventType, string resourceType, Guid resourceUid, Guid patientUid,
            string requestCorrelationId, CancellationToken cancellationToken = default)
        {
            Calls++;
            Recorded = (eventType, resourceType, resourceUid, patientUid, requestCorrelationId);
            return Failure is null ? Task.FromResult(Guid.NewGuid()) : Task.FromException<Guid>(Failure);
        }
        public Task<Guid> RecordAggregateReportAsync(string eventType, string reportKey,
            string requestCorrelationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class Files : IPatientFileService
    {
        private readonly Func<PatientFileContent?> content;
        public Files(PatientFileContent? content) : this(() => content) { }
        public Files(Func<PatientFileContent?> content) => this.content = content;
        public Task<PatientFileContent?> OpenContentAsync(Guid patientUid, Guid fileUid,
            CancellationToken cancellationToken = default) => Task.FromResult(content());
        public Task<IReadOnlyList<PatientFileResponse>> GetByPatientUidAsync(Guid patientUid,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PatientFileResponse>>([]);
        public Task<PatientFileResponse?> GetByUidAsync(Guid patientUid, Guid fileUid,
            CancellationToken cancellationToken = default) => Task.FromResult<PatientFileResponse?>(null);
        public Task<PatientFileResponse> UploadAsync(Guid patientUid, UploadPatientFileInput input,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientFileResponse> ArchiveAsync(Guid patientUid, Guid fileUid, string rowVersion,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientFileResponse> RestoreAsync(Guid patientUid, Guid fileUid, string rowVersion,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
