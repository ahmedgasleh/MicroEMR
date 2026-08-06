using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.PatientTasks;
using MicroEMR.Web.Controllers;
using MicroEMR.Web.Models.PatientTasks;
using MicroEMR.Web.Services.PatientTasks;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class NotificationsStabilizationTests
{
    [Fact]
    public void CountAndListUseIdenticalCanonicalPredicatesAndExistingIndex()
    {
        var migration = Read("db", "tenant-clinical", "migrations", "0030-overdue-task-foundation.sql");
        var countProcedure = Procedure(migration, "dbo.PatientTask_GetOverdueCount");
        var listProcedure = Procedure(migration, "dbo.PatientTask_GetOverdue");

        Assert.Equal(Predicate(countProcedure), Predicate(listProcedure));
        Assert.Contains("t.TaskStatus = N'Open'", Predicate(countProcedure));
        Assert.Contains("t.DueAt IS NOT NULL", Predicate(countProcedure));
        Assert.Contains("t.DueAt < SYSUTCDATETIME()", Predicate(countProcedure));
        Assert.Contains("t.AssignedTo = @AssignedTo OR t.AssignedTo IS NULL", Predicate(countProcedure));

        var canonicalTasks = Read("db", "patient_task_stored_procedures.sql");
        Assert.Contains("IX_PatientTask_AssignedTo_Status_DueAt", canonicalTasks);
    }

    [Fact]
    public void ExistingTaskMutationsNaturallyEnterAndLeaveOverdueScope()
    {
        var sql = Read("db", "patient_task_stored_procedures.sql");
        Assert.Contains("TaskStatus=N'Completed',CompletedAt=SYSUTCDATETIME()", sql);
        Assert.Contains("TaskStatus=N'Open',CompletedAt=NULL", sql);
        Assert.Contains("TaskPriority=@TaskPriority,DueAt=@DueAt,AssignedTo=@AssignedTo", sql);

        var now = new DateTime(2026, 8, 6, 16, 0, 0, DateTimeKind.Utc);
        Assert.True(PatientTaskOverdueRule.IsOverdue(now.AddMinutes(-1), "Open", now));
        Assert.False(PatientTaskOverdueRule.IsOverdue(now.AddMinutes(1), "Open", now));
        Assert.False(PatientTaskOverdueRule.IsOverdue(null, "Open", now));
        Assert.False(PatientTaskOverdueRule.IsOverdue(now.AddMinutes(-1), "Completed", now));
    }

    [Fact]
    public void ServiceUsesClinicalActorAndNeverParsesBrowserSubjectOrTenant()
    {
        var service = Read("src", "MicroEMR.Application", "PatientTasks", "PatientTaskOverdueService.cs");
        Assert.Contains("GetRequiredUserIdAsync", service);
        Assert.DoesNotContain("FindFirst", service);
        Assert.DoesNotContain("long.Parse", service);
        Assert.DoesNotContain("TenantUid", service);

        var script = Read("src", "MicroEMR.Web", "ClientApp", "overdue-task-indicator.ts");
        Assert.Equal(1, Occurrences(script, "fetch("));
        Assert.DoesNotContain("setInterval", script);
        Assert.DoesNotContain("localStorage", script);
        Assert.DoesNotContain("sessionStorage", script);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task WebEndpointSuppressesUpstreamFailureDetails(HttpStatusCode status)
    {
        var controller = new PatientTasksController(
            new FailingTaskClient(new HttpRequestException("sensitive upstream detail", null, status)),
            NullLogger<PatientTasksController>.Instance);

        var result = Assert.IsType<StatusCodeResult>(await controller.OverdueCount());
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
    }

    [Fact]
    public async Task WebEndpointHandlesCancellationWithoutBreakingLayoutContract()
    {
        var controller = new PatientTasksController(
            new FailingTaskClient(new OperationCanceledException()),
            NullLogger<PatientTasksController>.Instance);

        var result = Assert.IsType<StatusCodeResult>(await controller.OverdueCount());
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
    }

    private sealed class FailingTaskClient(Exception exception) : IPatientTaskApiClient
    {
        public Task<int> GetOverdueCountAsync(CancellationToken cancellationToken = default) => Task.FromException<int>(exception);
        public Task<IReadOnlyList<PatientTaskViewModel>> GetPatientTasksAsync(Guid patientUid, string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientTaskViewModel?> GetPatientTaskAsync(Guid patientUid, Guid uid, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PatientTaskViewModel?> CreatePatientTaskAsync(Guid patientUid, object request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PatientTaskViewModel?> UpdatePatientTaskAsync(Guid patientUid, Guid uid, object request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PatientTaskViewModel?> CompletePatientTaskAsync(Guid patientUid, Guid uid, object request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PatientTaskViewModel?> ReopenPatientTaskAsync(Guid patientUid, Guid uid, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<PatientDashboardTaskViewModel>> GetDashboardOpenTasksAsync(int maxRows, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static string Procedure(string sql, string name)
    {
        var start = sql.IndexOf($"CREATE OR ALTER PROCEDURE {name}", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = sql.IndexOf("\nGO", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return sql[start..end];
    }

    private static string Predicate(string procedure)
    {
        var start = procedure.IndexOf("WHERE ", StringComparison.Ordinal) + "WHERE ".Length;
        var order = procedure.IndexOf("ORDER BY", start, StringComparison.Ordinal);
        var end = order >= 0 ? order : procedure.IndexOf(';', start);
        return string.Join(' ', procedure[start..end].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).TrimEnd(';');
    }

    private static int Occurrences(string value, string fragment) =>
        (value.Length - value.Replace(fragment, string.Empty, StringComparison.Ordinal).Length) / fragment.Length;
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string file = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", ".."));
}
