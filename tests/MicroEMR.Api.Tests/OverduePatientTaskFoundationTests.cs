using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientTasks;
using MicroEMR.Infrastructure.PatientTasks;
using MicroEMR.Infrastructure.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class OverduePatientTaskFoundationTests
{
    private static readonly DateTime Now = new(2026, 8, 6, 16, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-1, "Open", true)]
    [InlineData(0, "Open", false)]
    [InlineData(1, "Open", false)]
    [InlineData(-1, "Completed", false)]
    public void CanonicalRuleRequiresPastDueAndOpen(int secondsFromNow, string status, bool expected) =>
        Assert.Equal(expected, PatientTaskOverdueRule.IsOverdue(Now.AddSeconds(secondsFromNow), status, Now));

    [Fact]
    public void CanonicalRuleExcludesTasksWithoutDueDate() =>
        Assert.False(PatientTaskOverdueRule.IsOverdue(null, "Open", Now));

    [Fact]
    public void SqlUsesSameUtcStatusBoundaryOwnershipAndOrderingForCountAndList()
    {
        var sql = File.ReadAllText(Path.Combine(Root(), "db", "tenant-clinical", "migrations", "0030-overdue-task-foundation.sql"));
        Assert.Equal(2, Count(sql, "t.TaskStatus = N'Open'"));
        Assert.Equal(2, Count(sql, "t.DueAt < SYSUTCDATETIME()"));
        Assert.Equal(2, Count(sql, "t.DueAt IS NOT NULL"));
        Assert.Equal(2, Count(sql, "t.AssignedTo = @AssignedTo OR t.AssignedTo IS NULL"));
        Assert.Equal(2, Count(sql, "p.IsDeleted = 0"));
        Assert.Contains("ORDER BY t.DueAt, t.PatientTaskId", sql);
        Assert.DoesNotContain("TenantUid", sql);
        Assert.DoesNotContain("TenantKey", sql);
    }

    [Fact]
    public async Task ApplicationServiceScopesBothOperationsToAuthenticatedClinicalUser()
    {
        var repository = new RecordingRepository();
        var service = new PatientTaskOverdueService(repository, new Actor(42));
        Assert.Equal(1, await service.GetOverdueCountAsync());
        Assert.Single(await service.GetOverdueAsync());
        Assert.Equal([42L, 42L], repository.AssignedToValues);
    }

    [Fact]
    public void RepositoryUsesSelectedTenantConnectionAndDoesNotAcceptTenantFromClient()
    {
        Assert.Contains(typeof(PatientTaskRepository).GetConstructors().Single().GetParameters(), p => p.ParameterType == typeof(ITenantSqlConnectionFactory));
        Assert.Equal([typeof(long), typeof(CancellationToken)], typeof(IPatientTaskRepository).GetMethod(nameof(IPatientTaskRepository.GetOverdueCountAsync))!.GetParameters().Select(p => p.ParameterType));
        Assert.Equal([typeof(long), typeof(CancellationToken)], typeof(IPatientTaskRepository).GetMethod(nameof(IPatientTaskRepository.GetOverdueAsync))!.GetParameters().Select(p => p.ParameterType));
        Assert.DoesNotContain(typeof(PatientTaskDashboardController).GetMethods().SelectMany(m => m.GetParameters()), p => p.Name?.Contains("tenant", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void EndpointsAreAuthenticatedReadOnlyAndNarrow()
    {
        Assert.NotEmpty(typeof(PatientTaskDashboardController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        var count = typeof(PatientTaskDashboardController).GetMethod(nameof(PatientTaskDashboardController.OverdueCount))!;
        var list = typeof(PatientTaskDashboardController).GetMethod(nameof(PatientTaskDashboardController.Overdue))!;
        Assert.Equal("overdue/count", count.GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>().Single().Template);
        Assert.Equal("overdue", list.GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>().Single().Template);
        Assert.Empty(count.GetCustomAttributes(typeof(HttpPostAttribute), true));
        Assert.Empty(list.GetCustomAttributes(typeof(HttpPostAttribute), true));
    }

    private sealed class Actor(long userId) : IAuthenticatedClinicalUserAccessor
    {
        public Task<long> GetRequiredUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(userId);
    }

    private sealed class RecordingRepository : IPatientTaskRepository
    {
        public List<long> AssignedToValues { get; } = [];
        public Task<int> GetOverdueCountAsync(long assignedTo, CancellationToken cancellationToken = default) { AssignedToValues.Add(assignedTo); return Task.FromResult(1); }
        public Task<IReadOnlyList<OverduePatientTaskItem>> GetOverdueAsync(long assignedTo, CancellationToken cancellationToken = default) { AssignedToValues.Add(assignedTo); return Task.FromResult<IReadOnlyList<OverduePatientTaskItem>>([new()]); }
        public Task<IReadOnlyList<PatientTaskResponse>> GetByPatientUidAsync(Guid p, string s, CancellationToken c = default) => throw new NotSupportedException();
        public Task<PatientTaskResponse?> GetByUidAsync(Guid p, Guid t, CancellationToken c = default) => throw new NotSupportedException();
        public Task<PatientTaskResponse?> CreateAsync(Guid p, CreatePatientTaskRequest r, long? u, CancellationToken c = default) => throw new NotSupportedException();
        public Task<PatientTaskResponse?> UpdateAsync(Guid p, Guid t, UpdatePatientTaskRequest r, long? u, CancellationToken c = default) => throw new NotSupportedException();
        public Task<PatientTaskResponse?> CompleteAsync(Guid p, Guid t, CompletePatientTaskRequest r, long? u, CancellationToken c = default) => throw new NotSupportedException();
        public Task<PatientTaskResponse?> ReopenAsync(Guid p, Guid t, long? u, CancellationToken c = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PatientDashboardTaskResponse>> GetOpenForDashboardAsync(long? u, int m, CancellationToken c = default) => throw new NotSupportedException();
    }

    private static int Count(string value, string fragment) => (value.Length - value.Replace(fragment, "", StringComparison.Ordinal).Length) / fragment.Length;
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string file = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", ".."));
}
