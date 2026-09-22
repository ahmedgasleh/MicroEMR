using System.Data;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Infrastructure.PatientResults;
using MicroEMR.Web.Services.PatientResults;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class ResultHistoryRepairTests
{
    [Fact]
    public void HistoryMapsOriginalAndCorrectionWithoutUnselectedUpdaterDisplayName()
    {
        using var table = HistoryTable();
        var patient = Guid.NewGuid();
        var original = Guid.NewGuid();
        var correction = Guid.NewGuid();
        AddRow(table, patient, original, null, "Superseded", "Reviewed");
        AddRow(table, patient, correction, original, "Current", "New");
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        var first = PatientResultRepository.MapHistory(reader);
        Assert.Equal(original, first.PatientResultUid);
        Assert.Null(first.PreviousResultUid);
        Assert.Equal("Superseded", first.LifecycleStatus);
        Assert.Equal("Reviewed", first.ResultStatus);
        Assert.Equal("Reviewer", first.ReviewedByDisplayName);
        Assert.Equal(42L, first.UpdatedBy);
        Assert.NotNull(first.UpdatedAt);
        Assert.Null(first.UpdatedByDisplayName);
        Assert.True(reader.Read());
        var next = PatientResultRepository.MapHistory(reader);
        Assert.Equal(correction, next.PatientResultUid);
        Assert.Equal(patient, next.PatientUid);
        Assert.Equal(original, next.PreviousResultUid);
        Assert.Equal("Current", next.LifecycleStatus);
        Assert.Equal("New", next.ResultStatus);
        Assert.Equal("External", next.SourceType);
        Assert.Equal("Synthetic laboratory", next.SourceOrganization);
        Assert.Equal("Synthetic system", next.SourceSystem);
        Assert.Equal("external-123", next.ExternalResultId);
        Assert.NotNull(next.ReceivedAtUtc);
        Assert.Null(next.ReviewedAt);
        Assert.Null(next.ReviewedBy);
        Assert.Equal(Convert.ToBase64String(new byte[8]), next.RowVersion);
        Assert.False(next.ReviewWasApplied);
        Assert.False(reader.Read());
    }

    [Fact]
    public void HistoryStillRequiresLineageColumns()
    {
        using var table = HistoryTable();
        AddRow(table, Guid.NewGuid(), Guid.NewGuid(), null, "Current", "New");
        table.Columns.Remove("PreviousResultUid");
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        Assert.Throws<ArgumentException>(() => PatientResultRepository.MapHistory(reader));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    public void HistoryFailureIsBoundedAndPreservesAccessStatus(int status)
    {
        const string secret = "SELECT secret; C:\\source\\result.cs:line 123\n at Internal.Map()\n HEADERS=== Cookie: .AspNetCore.Cookies=ticket-secret Authorization: Bearer token-secret";
        var http = new DefaultHttpContext { TraceIdentifier = "history-support-id" };
        http.Request.Headers.Cookie = ".AspNetCore.Cookies=ticket-secret";
        http.Request.Headers.Authorization = "Bearer token-secret";
        var context = new ExceptionContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), [])
        {
            Exception = new HttpRequestException(secret, null, (HttpStatusCode)status)
        };
        new ResultHistoryExceptionFilter(NullLogger<ResultHistoryExceptionFilter>.Instance).OnException(context);
        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<JsonResult>(context.Result);
        Assert.Equal(status, result.StatusCode);
        Assert.Equal("application/problem+json", result.ContentType);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<string>(problem.Extensions["traceId"])));
        var body = JsonSerializer.Serialize(problem);
        foreach (var forbidden in new[] { "SELECT", "source", "Internal.Map", "HEADERS", "Cookie", ".AspNetCore", "ticket-secret", "Authorization", "token-secret", "HttpRequestException" })
            Assert.DoesNotContain(forbidden, body);
        var attribute = Assert.Single(typeof(MicroEMR.Web.Controllers.PatientResultsController)
            .GetMethod("History")!.GetCustomAttributes(typeof(TypeFilterAttribute), false));
        Assert.Equal(typeof(ResultHistoryExceptionFilter), ((TypeFilterAttribute)attribute).ImplementationType);
    }

    private static DataTable HistoryTable()
    {
        var table = new DataTable();
        foreach (var name in new[] { "PatientResultUid", "PatientUid", "PreviousResultUid" }) table.Columns.Add(name, typeof(Guid));
        foreach (var name in new[] { "ResultType", "ResultName", "ResultSummary", "ResultValue", "ResultUnit", "ReferenceRange", "ResultStatus", "LifecycleStatus", "SourceType", "SourceOrganization", "SourceSystem", "ExternalResultId", "Abnormality", "EnteredInErrorByDisplayName", "EnteredInErrorReason", "ReviewedByDisplayName", "ReviewNote", "CreatedByDisplayName" }) table.Columns.Add(name, typeof(string));
        foreach (var name in new[] { "ResultDate", "ReceivedAtUtc", "EnteredInErrorAtUtc", "ReviewedAt", "CreatedAt", "UpdatedAt" }) table.Columns.Add(name, typeof(DateTime));
        foreach (var name in new[] { "EnteredInErrorBy", "ReviewedBy", "CreatedBy", "UpdatedBy" }) table.Columns.Add(name, typeof(long));
        table.Columns.Add("RowVersion", typeof(byte[]));
        return table;
    }

    private static void AddRow(DataTable table, Guid patient, Guid uid, Guid? previous, string lifecycle, string status)
    {
        var row = table.NewRow();
        row["PatientUid"] = patient;
        row["PatientResultUid"] = uid;
        row["PreviousResultUid"] = (object?)previous ?? DBNull.Value;
        row["ResultType"] = "Lab";
        row["ResultName"] = "Synthetic result";
        row["ResultDate"] = row["CreatedAt"] = row["UpdatedAt"] = row["ReceivedAtUtc"] = new DateTime(2026, 9, 22);
        row["LifecycleStatus"] = lifecycle;
        row["ResultStatus"] = status;
        row["SourceType"] = "External";
        row["SourceOrganization"] = "Synthetic laboratory";
        row["SourceSystem"] = "Synthetic system";
        row["ExternalResultId"] = "external-123";
        row["Abnormality"] = "Normal";
        row["CreatedBy"] = row["UpdatedBy"] = 42L;
        row["CreatedByDisplayName"] = "Creator";
        if (status == "Reviewed")
        {
            row["ReviewedAt"] = new DateTime(2026, 9, 22);
            row["ReviewedBy"] = 42L;
            row["ReviewedByDisplayName"] = "Reviewer";
        }
        row["RowVersion"] = new byte[8];
        table.Rows.Add(row);
    }
}
