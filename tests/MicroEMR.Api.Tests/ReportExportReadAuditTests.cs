using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.Reporting;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ReportExportReadAuditTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly End = new(2026, 1, 31);

    [Fact]
    public async Task ExplicitReportRunCreatesExactlyOneGovernedAggregateEvent()
    {
        var audit = new Audit();
        var controller = Controller(new Reports(), audit);

        var result = await controller.Get(Start, End, true);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, audit.Calls);
        Assert.Equal((ReadAuditActions.ReportExecuted, ReadAuditReportKeys.AppointmentStatusDateReport,
            "step17b-trace"), audit.Recorded);
    }

    [Fact]
    public async Task InitialPageReportLoadDoesNotCreateExecutionEvent()
    {
        var audit = new Audit();
        var controller = Controller(new Reports(), audit);

        Assert.IsType<OkObjectResult>((await controller.Get(Start, End, false)).Result);
        Assert.Equal(0, audit.Calls);
    }

    [Fact]
    public async Task CsvCreatesOnlyCsvExportedAfterGeneration()
    {
        var reports = new Reports();
        var audit = new Audit();
        var controller = Controller(reports, audit);

        var result = Assert.IsType<FileContentResult>(await controller.Csv(Start, End, default));

        Assert.NotEmpty(result.FileContents);
        Assert.Equal(1, reports.CsvCalls);
        Assert.Equal(1, audit.Calls);
        Assert.Equal(ReadAuditActions.CsvExported, audit.Recorded.Event);
        Assert.DoesNotContain(audit.Events, x => x == ReadAuditActions.ReportExecuted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuditFailurePreventsReportOrCsvResponse(bool csv)
    {
        var controller = Controller(new Reports(), new Audit { Failure = new InvalidOperationException("audit unavailable") });

        var result = csv
            ? await controller.Csv(Start, End, default)
            : (await controller.Get(Start, End, true)).Result!;

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task FailedQueryCreatesNoSuccessfulEvent()
    {
        var audit = new Audit();
        var controller = Controller(new Reports { Failure = new InvalidOperationException("query failed") }, audit);

        var result = await controller.Get(Start, End, true);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Equal(0, audit.Calls);
    }

    [Fact]
    public async Task RepeatedExplicitActionsCreateSeparateEvents()
    {
        var audit = new Audit();
        var controller = Controller(new Reports(), audit);

        await controller.Get(Start, End, true);
        await controller.Get(Start, End, true);
        await controller.Csv(Start, End, default);
        await controller.Csv(Start, End, default);

        Assert.Equal(4, audit.Calls);
        Assert.Equal(2, audit.Events.Count(x => x == ReadAuditActions.ReportExecuted));
        Assert.Equal(2, audit.Events.Count(x => x == ReadAuditActions.CsvExported));
    }

    [Fact]
    public void RepositoryUsesNullPatientAndResourceAndNoReportContentParameters()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Infrastructure", "ReadAudit",
            "ReadAuditRepository.cs"));
        var method = source[source.IndexOf("RecordAggregateReportAsync", StringComparison.Ordinal)..];
        Assert.Equal(2, method.Split("DBNull.Value", StringSplitOptions.None).Length - 1);
        Assert.Contains("@ReportKey", method);
        foreach (var forbidden in new[] { "StartDate", "EndDate", "PatientName", "ReportRows", "CsvContent" })
            Assert.DoesNotContain(forbidden, method, StringComparison.OrdinalIgnoreCase);
    }

    private static AppointmentReportsController Controller(IAppointmentStatusReportService reports,
        IStructuredReadAuditService audit)
    {
        var controller = new AppointmentReportsController(reports, audit,
            NullLogger<AppointmentReportsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "step17b-trace" }
        };
        return controller;
    }

    private static AppointmentStatusReport Result() => new(Start, End, "UTC", 0, [], []);
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));

    private sealed class Reports : IAppointmentStatusReportService
    {
        public Exception? Failure { get; init; }
        public int CsvCalls { get; private set; }
        public Task<AppointmentStatusReport> GetAsync(DateOnly startDate, DateOnly endDate,
            CancellationToken cancellationToken = default) => Failure is null
                ? Task.FromResult(Result())
                : Task.FromException<AppointmentStatusReport>(Failure);
        public byte[] CreateCsv(AppointmentStatusReport report)
        {
            CsvCalls++;
            return [1, 2, 3];
        }
    }

    private sealed class Audit : IStructuredReadAuditService
    {
        public int Calls { get; private set; }
        public List<string> Events { get; } = [];
        public (string Event, string ReportKey, string Correlation) Recorded { get; private set; }
        public Exception? Failure { get; init; }
        public Task<Guid> RecordAggregateReportAsync(string eventType, string reportKey,
            string requestCorrelationId, CancellationToken cancellationToken = default)
        {
            Calls++;
            Events.Add(eventType);
            Recorded = (eventType, reportKey, requestCorrelationId);
            return Failure is null ? Task.FromResult(Guid.NewGuid()) : Task.FromException<Guid>(Failure);
        }
        public Task<Guid> RecordAsync(string eventType, string resourceType, Guid resourceUid, Guid patientUid,
            string requestCorrelationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
