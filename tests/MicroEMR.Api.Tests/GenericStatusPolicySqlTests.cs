using System.Data;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Services;
using MicroEMR.Infrastructure.Scheduling;
using MicroEMR.Web.Services.Scheduling;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class GenericStatusPolicySqlTests(NextAvailableSqlDatabase db) : IClassFixture<NextAvailableSqlDatabase>
{
    private SchedulingAppointmentService Service => new(db.Appointments, new AppointmentStatusTransitionService());
    [SchedulingSearchSqlFact]
    public Task ScheduledToCompletedLeavesPersistedStateAuditAndHistoryUnchanged() => Reject("Scheduled", "Completed");
    [SchedulingSearchSqlFact]
    public Task CompletedToScheduledLeavesPersistedStateAuditAndHistoryUnchanged() => Reject("Completed", "Scheduled");
    private async Task Reject(string source, string target)
    {
        var uid = await Appointment();
        if (source != "Scheduled") await Execute("UPDATE dbo.ScheduleAppointment SET AppointmentStatus=@status WHERE AppointmentUid=@p", uid, source);
        var before = await Snapshot(uid);
        await Assert.ThrowsAsync<AppointmentStatusTransitionException>(() => Service.UpdateStatusAsync(uid, new() { Status = target }, 7));
        Assert.Equal(before, await Snapshot(uid));
    }

    [SchedulingSearchSqlFact]
    public async Task AllowedStatusChainWritesExactlyOneAttributedAuditAndHistoryPerChange()
    {
        var uid = await Appointment();
        foreach (var target in new[] { "Arrived", "Seen", "Completed" })
            Assert.Equal(target, (await Service.UpdateStatusAsync(uid, new() { Status = target }, 7))!.Status);
        Assert.Equal(3, await Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p) AND ActionName='UpdateStatus' AND UserId=7", uid));
        Assert.Equal(3, await Scalar<int>("SELECT COUNT(*) FROM dbo.AppointmentHistory WHERE AppointmentUid=@p AND ActionType='StatusChanged' AND CreatedBy=7", uid));
        var before = await Snapshot(uid);
        await Assert.ThrowsAsync<AppointmentStatusTransitionException>(() => Service.UpdateStatusAsync(uid, new() { Status = "Completed" }, 7));
        Assert.Equal(before, await Snapshot(uid));
    }

    [SchedulingSearchSqlFact]
    public async Task CompetingExpectedStateUpdatesHaveOneWinnerAndNoFalseSuccessAudit()
    {
        var uid = await Appointment();
        async Task<bool> Attempt(string target)
        {
            try { await db.Appointments.UpdateStatusAsync(uid, new() { Status = target }, AppointmentStatus.Scheduled, 7); return true; }
            catch (AppointmentStatusConcurrencyException) { return false; }
        }
        var outcomes = await Task.WhenAll(Attempt("Arrived"), Attempt("Seen"));
        Assert.Single(outcomes, x => x);
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p) AND ActionName='UpdateStatus'", uid));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM dbo.AppointmentHistory WHERE AppointmentUid=@p AND ActionType='StatusChanged'", uid));
        var before = await Snapshot(uid);
        await Assert.ThrowsAsync<AppointmentStatusConcurrencyException>(() => db.Appointments.UpdateStatusAsync(uid, new() { Status = "Seen" }, AppointmentStatus.Scheduled, 7));
        Assert.Equal(before, await Snapshot(uid));
    }

    [SchedulingSearchSqlFact]
    public async Task WriterLockWaitsThenRejectsChangedSourceWithoutOverwritingNewerState()
    {
        var uid = await Appointment();
        await using var connection = new SqlConnection(db.Connection); await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var writer = new SqlCommand("UPDATE dbo.ScheduleAppointment SET AppointmentStatus='Completed' WHERE AppointmentUid=@p", connection, transaction);
        writer.Parameters.AddWithValue("@p", uid); await writer.ExecuteNonQueryAsync();
        var attempt = db.Appointments.UpdateStatusAsync(uid, new() { Status = "Arrived" }, AppointmentStatus.Scheduled, 7);
        await Task.Delay(150); Assert.False(attempt.IsCompleted);
        await transaction.CommitAsync();
        await Assert.ThrowsAsync<AppointmentStatusConcurrencyException>(() => attempt);
        Assert.Equal("Completed", await Scalar<string>("SELECT AppointmentStatus FROM dbo.ScheduleAppointment WHERE AppointmentUid=@p", uid));
        Assert.Equal(0, await Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p) AND ActionName='UpdateStatus'", uid));
        Assert.Equal(0, await Scalar<int>("SELECT COUNT(*) FROM dbo.AppointmentHistory WHERE AppointmentUid=@p AND ActionType='StatusChanged'", uid));
    }

    [SchedulingSearchSqlFact]
    public async Task LegacyBookedMapsToScheduledWithoutChangingItsHistoricalOldValue()
    {
        var uid = await Appointment();
        await Execute("UPDATE dbo.ScheduleAppointment SET AppointmentStatus='Booked' WHERE AppointmentUid=@p", uid);
        Assert.Equal("Arrived", (await Service.UpdateStatusAsync(uid, new() { Status = "arrived" }, 7))!.Status);
        Assert.Equal("Booked", await Scalar<string>("SELECT TOP(1) OldValue FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p) AND ActionName='UpdateStatus'", uid));
    }

    [SchedulingSearchSqlFact]
    public async Task HistoryFailureRollsBackStatusAndItsEarlierAuditInsert()
    {
        var uid = await Appointment(); var before = await Snapshot(uid);
        await Execute("CREATE TRIGGER dbo.StatusHistoryFailure ON dbo.AppointmentHistory AFTER INSERT AS THROW 51999,'TEST ONLY history failure',1;");
        try
        {
            await Assert.ThrowsAsync<SqlException>(() => Service.UpdateStatusAsync(uid, new() { Status = "Arrived" }, 7));
            Assert.Equal(before, await Snapshot(uid));
        }
        finally { await Execute("DROP TRIGGER dbo.StatusHistoryFailure;"); }
    }

    [SchedulingSearchSqlFact]
    public async Task OtherTenantCannotReadOrChangeAppointmentOrCreateSuccessRecords()
    {
        var uid = await Appointment(); var before = await Snapshot(uid);
        var other = new SchedulingAppointmentRepository(new NextAvailableSqlDatabase.Factory(db.OtherConnection));
        Assert.Null(await new SchedulingAppointmentService(other, new AppointmentStatusTransitionService()).UpdateStatusAsync(uid, new() { Status = "Arrived" }, 7));
        Assert.Null(await other.UpdateStatusAsync(uid, new() { Status = "Arrived" }, AppointmentStatus.Scheduled, 7));
        Assert.Equal(before, await Snapshot(uid));
        Assert.Equal(0, await Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p)", uid, db.OtherConnection));
        Assert.Equal(0, await Scalar<int>("SELECT COUNT(*) FROM dbo.AppointmentHistory WHERE AppointmentUid=@p", uid, db.OtherConnection));
    }

    private async Task<Guid> Appointment()
    {
        var (patient, provider, _) = await db.Seed();
        return (await db.Appointments.CreateAsync(new()
        {
            PatientUid = patient, PrimaryResourceUid = provider,
            StartDateTimeUtc = new(2030, 1, 4, 9, 0, 0, DateTimeKind.Utc), EndDateTimeUtc = new(2030, 1, 4, 9, 30, 0, DateTimeKind.Utc)
        }, 7)).AppointmentUid;
    }
    private async Task Execute(string sql, Guid? uid = null, string? status = null)
    {
        await using var connection = new SqlConnection(db.Connection); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        if (uid.HasValue) command.Parameters.AddWithValue("@p", uid.Value);
        if (status is not null) command.Parameters.AddWithValue("@status", status);
        await command.ExecuteNonQueryAsync();
    }
    private async Task<T> Scalar<T>(string sql, Guid uid, string? connectionString = null)
    {
        await using var connection = new SqlConnection(connectionString ?? db.Connection); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@p", uid);
        return (T)(await command.ExecuteScalarAsync())!;
    }
    private Task<string> Snapshot(Guid uid) => Scalar<string>("SELECT (SELECT * FROM dbo.ScheduleAppointment WHERE AppointmentUid=@p FOR JSON PATH) Appointment,(SELECT * FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p) ORDER BY AuditLogId FOR JSON PATH) Audit,(SELECT * FROM dbo.AppointmentHistory WHERE AppointmentUid=@p ORDER BY AppointmentHistoryId FOR JSON PATH) History FOR JSON PATH,WITHOUT_ARRAY_WRAPPER", uid);
}

public sealed class GenericStatusPolicyBoundaryTests
{
    [Fact]
    public async Task ExistingManagementPolicyStillDeniesSchedulingViewOnlyAndAllowsManager()
    {
        var controller = typeof(MicroEMR.Api.Controllers.SchedulingController);
        var action = controller.GetMethod("UpdateAppointmentStatus")!;
        var permissions = controller.GetCustomAttributes<RequirePermissionAttribute>().Concat(action.GetCustomAttributes<RequirePermissionAttribute>()).Select(x => x.Policy).ToArray();
        Assert.Contains("Permission:" + PermissionKeys.SchedulingView, permissions);
        Assert.Contains("Permission:" + PermissionKeys.SchedulingManage, permissions);
        foreach (var manage in new[] { false, true })
        {
            var context = new AuthorizationHandlerContext(new[] { new PermissionRequirement(PermissionKeys.SchedulingView), new PermissionRequirement(PermissionKeys.SchedulingManage) },
                new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "test")], "Test")), null);
            await new PermissionAuthorizationHandler(new Permissions(manage), NullLogger<PermissionAuthorizationHandler>.Instance).HandleAsync(context);
            Assert.Equal(manage, context.HasSucceeded);
        }
    }
    [Fact]
    public async Task DashboardConflictIsSafeAndDoesNotClaimEveryRejectedTransitionIsCancellation()
    {
        var client = DispatchProxy.Create<ISchedulingApiClient, ConflictClient>();
        var controller = new MicroEMR.Web.Controllers.HomeController(client, null!, null!, NullLogger<MicroEMR.Web.Controllers.HomeController>.Instance);
        var result = await controller.UpdateAppointmentStatus(new() { AppointmentUid=Guid.NewGuid(), Status="Completed" }, default);
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("Refresh", conflict.Value!.ToString());
        Assert.DoesNotContain("Cancelled", conflict.Value.ToString());
        Assert.NotNull(typeof(MicroEMR.Web.Controllers.HomeController).GetMethod("UpdateAppointmentStatus")!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }
    public class ConflictClient : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "UpdateAppointmentStatusAsync"
            ? Task.FromException<MicroEMR.Web.Models.Scheduling.UpdateAppointmentStatusResponse?>(new AppointmentStatusConflictException())
            : throw new NotSupportedException();
    }
    private sealed class Permissions(bool manage) : ICurrentUserPermissionService
    {
        public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(CancellationToken token=default) =>
            Task.FromResult<IReadOnlySet<string>>(manage ? new HashSet<string> { PermissionKeys.SchedulingView, PermissionKeys.SchedulingManage } : new HashSet<string> { PermissionKeys.SchedulingView });
        public async Task<bool> HasPermissionAsync(string permissionKey, CancellationToken token=default) => (await GetEffectivePermissionsAsync(token)).Contains(permissionKey);
    }
}
