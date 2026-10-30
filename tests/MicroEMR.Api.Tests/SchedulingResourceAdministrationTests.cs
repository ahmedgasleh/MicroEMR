using System.Data;
using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Scheduling;
using MicroEMR.Infrastructure.Scheduling;
using MicroEMR.Infrastructure.Tenancy;
using MicroEMR.Web.Authorization;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class SchedulingResourceAdministrationTests(ResourceAdministrationDatabase db) : IClassFixture<ResourceAdministrationDatabase>
{
    [Fact]
    public void MigrationPreservesLegacyIdentifiersAndNamesWithoutAutomaticProviderLinks()
    {
        Assert.True(db.LegacyPreserved);
        Assert.Equal(4, db.LegacyResourceCount);
        Assert.Equal(0, db.InitialLinkedCount);
    }
    [Fact]
    public async Task ProviderResourceUsesExistingTenantProviderWithoutLoginAndAppearsInActiveRetrieval()
    {
        var provider = await db.Provider();
        var row = await db.Save("Provider", provider);
        Assert.Equal(provider, row.ProviderUid);
        Assert.True(row.IsActive);
        var resources = await new SchedulingReadRepository(new ResourceAdministrationDatabase.Factory(db.Connection),
            NullLogger<SchedulingReadRepository>.Instance).GetActiveResourcesAsync();
        Assert.Contains(resources, r => r.ResourceUid == row.ResourceUid);
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.ApplicationUser"));
    }

    [Fact]
    public async Task InvalidAndCrossTenantProviderCannotBeUsedAndDoNotAudit()
    {
        var foreignProvider = await db.Provider(other: true);
        var before = await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog");
        foreach (var uid in new[] { Guid.NewGuid(), foreignProvider })
            await Assert.ThrowsAsync<ArgumentException>(() => db.Save("Provider", uid));
        Assert.Equal(before, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog"));
        Assert.Null(await db.Repository(other: true).GetAsync((await db.Save("Room")).ResourceUid));
    }

    [Fact]
    public async Task ConcurrentDuplicateCreatesHaveOneWinnerAndOneSuccessAudit()
    {
        var provider = await db.Provider();
        async Task<bool> Attempt()
        {
            try { await db.Save("Provider", provider); return true; }
            catch (SchedulingResourceConflictException) { return false; }
        }
        var outcomes = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(outcomes, x => x);
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.ScheduleResource WHERE ProviderId=(SELECT ProviderId FROM dbo.Provider WHERE ProviderUid=@p) AND IsActive=1", provider));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE NewValue LIKE '%'+CONVERT(nvarchar(36),@p)+'%' AND ActionName='SchedulingResourceCreated'", provider));
    }

    [Theory]
    [InlineData("Room")]
    [InlineData("Equipment")]
    public async Task ResourcePropertiesEditAndStaleVersionRejectsWithoutExtraAudit(string type)
    {
        var row = await db.Save(type);
        var request = Request(row);
        request.DisplayName = "Changed display"; request.ColorCode = "#abcdef"; request.SortOrder = 73;
        var saved = (await db.Service().SaveAsync(row.ResourceUid, request, 7))!;
        Assert.Equal("Changed display", saved.DisplayName); Assert.Equal("#abcdef", saved.ColorCode); Assert.Equal(73, saved.SortOrder);
        Assert.Null(saved.ProviderUid); Assert.NotEqual(row.RowVersion, saved.RowVersion);
        var before = await db.AuditCount(row.ResourceUid);
        await Assert.ThrowsAsync<SchedulingResourceConflictException>(() => db.Service().SaveAsync(row.ResourceUid, request, 7));
        Assert.Equal(before, await db.AuditCount(row.ResourceUid));
    }

    [Fact]
    public async Task LegacyResourcesRemainUnlinkedUntilExplicitLinkAndCannotBeReassignedOrRetyped()
    {
        var legacy = (await db.Repository().ListAsync("All")).Single(x => x.DisplayName == "Dr. Smith");
        Assert.Null(legacy.ProviderUid);
        await db.Execute("INSERT dbo.ScheduleAppointment(PatientUid,PrimaryResourceId,StartDateTimeUtc,EndDateTimeUtc,AppointmentStatus) SELECT NEWID(),ResourceId,DATEADD(day,-2,SYSUTCDATETIME()),DATEADD(day,-1,SYSUTCDATETIME()),'Completed' FROM dbo.ScheduleResource WHERE ResourceUid=@p; INSERT dbo.AppointmentHistory(AppointmentUid,ActionType,ActionDescription,CreatedBy) SELECT AppointmentUid,'Created','TEST ONLY retained history',7 FROM dbo.ScheduleAppointment WHERE PrimaryResourceId=(SELECT ResourceId FROM dbo.ScheduleResource WHERE ResourceUid=@p); INSERT dbo.SchedulingBlockedTime(ResourceUid,StartDateTimeUtc,EndDateTimeUtc) VALUES(@p,SYSUTCDATETIME(),DATEADD(hour,1,SYSUTCDATETIME()));", legacy.ResourceUid);
        var history = await db.History(legacy.ResourceUid);
        var request = Request(legacy); request.DisplayName = "Legacy schedule";
        var edited = (await db.Service().SaveAsync(legacy.ResourceUid, request, 7))!;
        Assert.Null(edited.ProviderUid); Assert.Equal(legacy.ResourceUid, edited.ResourceUid);
        request = Request(edited); request.ProviderUid = await db.Provider();
        var linked = (await db.Service().SaveAsync(edited.ResourceUid, request, 7))!;
        Assert.Equal(request.ProviderUid, linked.ProviderUid);
        Assert.Equal(legacy.ResourceUid, linked.ResourceUid);
        Assert.Equal(history, await db.History(legacy.ResourceUid));
        Assert.Equal(1, await db.Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p) AND ActionName='SchedulingResourceProviderLinked'", linked.ResourceUid));
        foreach (var target in new Guid?[] { await db.Provider(), null })
        {
            request = Request(linked); request.ProviderUid = target;
            await Assert.ThrowsAsync<SchedulingResourceConflictException>(() => db.Service().SaveAsync(linked.ResourceUid, request, 7));
        }
        request = Request(linked); request.ResourceType = "Room"; request.ProviderUid = null;
        await Assert.ThrowsAsync<SchedulingResourceConflictException>(() => db.Service().SaveAsync(linked.ResourceUid, request, 7));
    }

    [Fact]
    public async Task DeactivationBlocksFuturePrimaryOrRoomAppointmentsButPreservesHistoryAndBlockedTime()
    {
        foreach (var roomReference in new[] { false, true })
        {
            var row = await db.Save(roomReference ? "Room" : "Equipment");
            await db.Execute("INSERT dbo.ScheduleAppointment(PatientUid,PrimaryResourceId,RoomResourceId,StartDateTimeUtc,EndDateTimeUtc,AppointmentStatus) SELECT NEWID(),CASE WHEN @room=1 THEN (SELECT TOP(1) ResourceId FROM dbo.ScheduleResource WHERE ResourceType='Provider' ORDER BY ResourceId) ELSE ResourceId END,CASE WHEN @room=1 THEN ResourceId END,DATEADD(day,1,SYSUTCDATETIME()),DATEADD(day,2,SYSUTCDATETIME()),'Scheduled' FROM dbo.ScheduleResource WHERE ResourceUid=@p", row.ResourceUid, roomReference);
            await Assert.ThrowsAsync<SchedulingResourceConflictException>(() => db.Service().SetActiveAsync(row.ResourceUid, false, row.RowVersion, 7));
            Assert.Equal(1, await db.AuditCount(row.ResourceUid));
            // Disposable fixture cancellation is setup, never application behavior or persistent data.
            await db.Execute("UPDATE dbo.ScheduleAppointment SET AppointmentStatus='Cancelled' WHERE PrimaryResourceId=(SELECT ResourceId FROM dbo.ScheduleResource WHERE ResourceUid=@p) OR RoomResourceId=(SELECT ResourceId FROM dbo.ScheduleResource WHERE ResourceUid=@p)", row.ResourceUid);
            await db.Execute("INSERT dbo.ScheduleAppointment(PatientUid,PrimaryResourceId,StartDateTimeUtc,EndDateTimeUtc,AppointmentStatus) SELECT NEWID(),ResourceId,DATEADD(day,-2,SYSUTCDATETIME()),DATEADD(day,-1,SYSUTCDATETIME()),'Completed' FROM dbo.ScheduleResource WHERE ResourceUid=@p; INSERT dbo.SchedulingBlockedTime(ResourceUid,StartDateTimeUtc,EndDateTimeUtc,Reason) VALUES(@p,SYSUTCDATETIME(),DATEADD(hour,1,SYSUTCDATETIME()),'TEST ONLY retained block');", row.ResourceUid);
            var before = await db.History(row.ResourceUid);
            var inactive = (await db.Service().SetActiveAsync(row.ResourceUid, false, row.RowVersion, 7))!;
            Assert.False(inactive.IsActive);
            Assert.Equal(before, await db.History(row.ResourceUid));
            var active = (await db.Service().SetActiveAsync(row.ResourceUid, true, inactive.RowVersion, 7))!;
            Assert.True(active.IsActive); Assert.Equal(before, await db.History(row.ResourceUid));
            await Assert.ThrowsAsync<SchedulingResourceConflictException>(() => db.Service().SetActiveAsync(row.ResourceUid, false, inactive.RowVersion, 7));
        }
    }

    [Fact]
    public async Task InactiveDuplicateCannotReactivateAndInactiveClinicalProviderCannotBeSelected()
    {
        var provider = await db.Provider(); var row = await db.Save("Provider", provider);
        var inactive = (await db.Service().SetActiveAsync(row.ResourceUid, false, row.RowVersion, 7))!;
        await db.Save("Provider", provider);
        await Assert.ThrowsAsync<SchedulingResourceConflictException>(() => db.Service().SetActiveAsync(row.ResourceUid, true, inactive.RowVersion, 7));
        var inactiveProvider = await db.Provider();
        await db.Execute("UPDATE dbo.Provider SET IsActive=0 WHERE ProviderUid=@p", inactiveProvider);
        Assert.DoesNotContain(await db.Service().ProvidersAsync(), x => x.ProviderUid == inactiveProvider);
        await Assert.ThrowsAsync<ArgumentException>(() => db.Save("Provider", inactiveProvider));
    }

    [Fact]
    public async Task DeactivationSerializesWithAppointmentWriterBeforeCheckingFutureAppointments()
    {
        var row = await db.Save("Room");
        await using var connection = new SqlConnection(db.Connection); await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var writer = new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=N'SchedulingAppointmentSave',@LockMode=N'Exclusive',@LockOwner=N'Transaction'; INSERT dbo.ScheduleAppointment(PatientUid,PrimaryResourceId,StartDateTimeUtc,EndDateTimeUtc,AppointmentStatus) SELECT NEWID(),ResourceId,DATEADD(day,1,SYSUTCDATETIME()),DATEADD(day,2,SYSUTCDATETIME()),'Scheduled' FROM dbo.ScheduleResource WHERE ResourceUid=@p;", connection, transaction);
        writer.Parameters.AddWithValue("@p", row.ResourceUid); await writer.ExecuteNonQueryAsync();
        var deactivation = db.Service().SetActiveAsync(row.ResourceUid, false, row.RowVersion, 7);
        await Task.Delay(150); Assert.False(deactivation.IsCompleted);
        await transaction.CommitAsync();
        await Assert.ThrowsAsync<SchedulingResourceConflictException>(() => deactivation);
        Assert.True((await db.Repository().GetAsync(row.ResourceUid))!.IsActive);
        Assert.Equal(1, await db.AuditCount(row.ResourceUid));
    }

    [Fact]
    public async Task ResourceAndActorCannotCrossTenantBoundaryAndAuditFailureRollsBack()
    {
        var row = await db.Save("Room");
        Assert.Null(await db.Repository(other: true).SaveAsync(row.ResourceUid, Request(row), 7));
        Assert.Null(await db.Repository(other: true).SetActiveAsync(row.ResourceUid, false, row.RowVersion, 7));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.Repository().SaveAsync(null, new() { ResourceType="Room", DisplayName="Denied" }, 99));
        var audit = await db.Scalar<string>("SELECT TOP(1) NewValue FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p) ORDER BY AuditLogId DESC", row.ResourceUid);
        using var snapshot = System.Text.Json.JsonDocument.Parse(audit);
        Assert.Equal(db.TenantUid, snapshot.RootElement.GetProperty("TenantUid").GetGuid());
        Assert.Equal(row.ResourceUid, snapshot.RootElement.GetProperty("ResourceUid").GetGuid());
        Assert.Equal(7L, await db.Scalar<long>("SELECT TOP(1) UserId FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p)", row.ResourceUid));
        await db.Execute("CREATE TRIGGER dbo.ResourceAuditFailure ON dbo.AuditLog AFTER INSERT AS THROW 51999,'TEST ONLY audit failure',1;");
        try
        {
            var change = Request(row); change.DisplayName="Must roll back";
            await Assert.ThrowsAsync<SqlException>(() => db.Service().SaveAsync(row.ResourceUid, change, 7));
            Assert.Equal(row, await db.Repository().GetAsync(row.ResourceUid));
        }
        finally { await db.Execute("DROP TRIGGER dbo.ResourceAuditFailure;"); }
    }

    private static SaveSchedulingResourceRequest Request(SchedulingResourceItem row) => new()
    { ResourceType=row.ResourceType, DisplayName=row.DisplayName, ColorCode=row.ColorCode, SortOrder=row.SortOrder, ProviderUid=row.ProviderUid, RowVersion=row.RowVersion };
}

public sealed class SchedulingResourceAdministrationValidationTests
{
    [Theory]
    [InlineData("Provider", null)]
    [InlineData("Other", null)]
    [InlineData("Room", "00000000-0000-0000-0000-000000000001")]
    public async Task InvalidTypeOrMissingOrInapplicableProviderRejectedBeforeRepository(string type, string? provider)
    {
        var service = new SchedulingResourceAdministrationService(new NeverRepository());
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(null, new()
        { ResourceType=type, DisplayName="Test", ProviderUid=provider is null ? null : Guid.Parse(provider) }, 7));
    }
    [Theory]
    [InlineData(null)] [InlineData("bad")] [InlineData("AQ==")]
    public async Task InvalidConcurrencyTokenRejectsBeforeRepository(string? version) =>
        await Assert.ThrowsAsync<ArgumentException>(() => new SchedulingResourceAdministrationService(new NeverRepository())
            .SetActiveAsync(Guid.NewGuid(), false, version!, 7));
    [Fact]
    public void BothHostsRequireManagementAndProviderReadAndAllWebMutationsRequireAntiforgery()
    {
        var api = typeof(MicroEMR.Api.Controllers.SchedulingResourcesController);
        var web = typeof(MicroEMR.Web.Controllers.SchedulingResourcesController);
        foreach (var type in new[] { api, web })
        {
            var policies = type.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy).Where(x => x is not null).ToArray();
            Assert.Contains(policies, p => p!.EndsWith(PermissionKeys.SchedulingManage, StringComparison.Ordinal));
            Assert.Contains(policies, p => p!.EndsWith(PermissionKeys.ProvidersView, StringComparison.Ordinal));
        }
        foreach (var action in new[] { "Save", "Activate", "Deactivate" })
            Assert.NotNull(web.GetMethod(action)!.GetCustomAttribute<Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute>());
        Assert.DoesNotContain(api.GetMethods(), x => x.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>() is not null);
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task BothHostPermissionHandlersDenyViewOnlyAndRequireBothPermissions(bool manage, bool providers)
    {
        var allowed = new HashSet<string> { PermissionKeys.SchedulingView };
        if (manage) allowed.Add(PermissionKeys.SchedulingManage);
        if (providers) allowed.Add(PermissionKeys.ProvidersView);
        var permissionService = new Permissions(allowed);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "test")], "Test"));
        var apiRequirements = new[] { new PermissionRequirement(PermissionKeys.SchedulingManage), new PermissionRequirement(PermissionKeys.ProvidersView) };
        var apiContext = new AuthorizationHandlerContext(apiRequirements, principal, null);
        await new PermissionAuthorizationHandler(permissionService, NullLogger<PermissionAuthorizationHandler>.Instance).HandleAsync(apiContext);
        Assert.Equal(manage && providers, apiContext.HasSucceeded);
        var webRequirements = new[] { new WebPermissionRequirement(PermissionKeys.SchedulingManage), new WebPermissionRequirement(PermissionKeys.ProvidersView) };
        var webContext = new AuthorizationHandlerContext(webRequirements, principal, null);
        await new WebPermissionHandler(permissionService).HandleAsync(webContext);
        Assert.Equal(manage && providers, webContext.HasSucceeded);
    }
    private sealed class Permissions(IReadOnlySet<string> allowed) : ICurrentUserPermissionService, IWebPermissionService
    {
        public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(CancellationToken token=default) => Task.FromResult(allowed);
        public Task<bool> HasPermissionAsync(string permissionKey, CancellationToken token=default) => Task.FromResult(allowed.Contains(permissionKey));
        public Task<IReadOnlySet<string>> GetAsync(CancellationToken token=default) => Task.FromResult(allowed);
        public Task<bool> HasAsync(string key, CancellationToken token=default) => Task.FromResult(allowed.Contains(key));
    }
    private sealed class NeverRepository : ISchedulingResourceAdministrationRepository
    {
        public Task<IReadOnlyList<SchedulingResourceItem>> ListAsync(string status, CancellationToken token=default) => throw new InvalidOperationException("Unexpected repository call");
        public Task<SchedulingResourceItem?> GetAsync(Guid uid, CancellationToken token=default) => throw new InvalidOperationException("Unexpected repository call");
        public Task<IReadOnlyList<SchedulingProviderChoice>> ProvidersAsync(CancellationToken token=default) => throw new InvalidOperationException("Unexpected repository call");
        public Task<SchedulingResourceItem?> SaveAsync(Guid? uid, SaveSchedulingResourceRequest request, long actor, CancellationToken token=default) => throw new InvalidOperationException("Unexpected repository call");
        public Task<SchedulingResourceItem?> SetActiveAsync(Guid uid, bool active, string version, long actor, CancellationToken token=default) => throw new InvalidOperationException("Unexpected repository call");
    }
}

public sealed class ResourceAdministrationDatabase : IAsyncLifetime
{
    private readonly string[] names = ["MicroEMR_ResourceAdmin_" + Guid.NewGuid().ToString("N"), "MicroEMR_ResourceAdmin_" + Guid.NewGuid().ToString("N")];
    private const string Master = @"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=5";
    private readonly HashSet<string> created = [];
    public Guid TenantUid { get; } = Guid.NewGuid();
    public bool LegacyPreserved { get; private set; }
    public int LegacyResourceCount { get; private set; }
    public int InitialLinkedCount { get; private set; }
    public string Connection => For(names[0]);
    private static string For(string name) => new SqlConnectionStringBuilder(Master) { InitialCatalog=name }.ConnectionString;
    public SchedulingResourceAdministrationRepository Repository(bool other=false) => new(new Factory(For(names[other ? 1 : 0])));
    public SchedulingResourceAdministrationService Service() => new(Repository());
    public async Task<SchedulingResourceItem> Save(string type, Guid? provider=null) => (await Service().SaveAsync(null, new()
        { ResourceType=type, DisplayName="TEST ONLY " + Guid.NewGuid().ToString("N"), ProviderUid=provider }, 7))!;
    public async Task<Guid> Provider(bool other=false)
    {
        var uid=Guid.NewGuid();
        await Execute("INSERT dbo.Provider(ProviderUid,DisplayName,IsActive) VALUES(@p,'TEST ONLY physician',1)", uid, other:other);
        return uid;
    }
    public async Task InitializeAsync()
    {
        try
        {
            var scheduling = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database/scheduling_stored_procedures.sql"));
            foreach (var (name, index) in names.Select((name,index)=>(name,index)))
            {
                await using (var master = new SqlConnection(Master)) { await master.OpenAsync(); await new SqlCommand($"CREATE DATABASE [{name}]", master).ExecuteNonQueryAsync(); created.Add(name); }
                await Execute("""
                    CREATE TABLE dbo.Provider(ProviderId bigint IDENTITY PRIMARY KEY,ProviderUid uniqueidentifier UNIQUE,DisplayName nvarchar(200),IsActive bit);
                    CREATE TABLE dbo.ApplicationUser(UserId bigint PRIMARY KEY,IsActive bit); INSERT dbo.ApplicationUser VALUES(7,1);
                    CREATE TABLE dbo.TenantDatabaseIdentity(TenantUid uniqueidentifier PRIMARY KEY,TenantKey nvarchar(50));
                    CREATE TABLE dbo.AuditLog(AuditLogId bigint IDENTITY PRIMARY KEY,UserId bigint,PatientId bigint,ActionName nvarchar(100),EntityName nvarchar(100),EntityId nvarchar(100),OldValue nvarchar(max),NewValue nvarchar(max),CreatedAt datetime2);
                    """, other:index==1);
                await Execute("INSERT dbo.TenantDatabaseIdentity VALUES(@p,'test-only')", index==0 ? TenantUid : Guid.NewGuid(), other:index==1);
                foreach (var batch in Regex.Split(scheduling, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    var text=batch.Trim();
                    if (text.StartsWith("IF OBJECT_ID(N'dbo.ScheduleResource', N'U')", StringComparison.Ordinal)
                        || text.StartsWith("IF OBJECT_ID(N'dbo.ScheduleAppointment', N'U')", StringComparison.Ordinal)
                        || text.StartsWith("IF OBJECT_ID(N'dbo.SchedulingBlockedTime', N'U')", StringComparison.Ordinal)
                        || text.StartsWith("IF OBJECT_ID(N'dbo.AppointmentHistory', N'U')", StringComparison.Ordinal)
                        || text.StartsWith("CREATE OR ALTER PROCEDURE dbo.ScheduleResource_GetActive", StringComparison.Ordinal)
                        || (text.StartsWith("IF NOT EXISTS", StringComparison.Ordinal) && text.Contains("INSERT INTO dbo.ScheduleResource", StringComparison.Ordinal)))
                        await Execute(text, other:index==1);
                }
                const string legacySnapshot = "SELECT ResourceId,ResourceUid,ResourceType,DisplayName,ColorCode,IsActive,SortOrder,CreatedAt FROM dbo.ScheduleResource ORDER BY ResourceId FOR JSON PATH";
                var before = index == 0 ? await Scalar<string>(legacySnapshot) : null;
                var migration=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"database/tenant-clinical/migrations/0101-scheduling-resource-administration.sql"));
                foreach (var batch in Regex.Split(migration,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase).Where(x=>!string.IsNullOrWhiteSpace(x)))
                    await Execute(batch, other:index==1);
                if (index == 0)
                {
                    LegacyPreserved = before == await Scalar<string>(legacySnapshot);
                    LegacyResourceCount = await Scalar<int>("SELECT COUNT(*) FROM dbo.ScheduleResource");
                    InitialLinkedCount = await Scalar<int>("SELECT COUNT(*) FROM dbo.ScheduleResource WHERE ProviderId IS NOT NULL");
                }
            }
        }
        catch { await DisposeAsync(); throw; }
    }
    public async Task Execute(string sql, Guid? uid=null, bool room=false, bool other=false)
    {
        await using var c=new SqlConnection(For(names[other?1:0])); await c.OpenAsync();
        await using var q=new SqlCommand(sql,c);
        if (sql.Contains("@p", StringComparison.Ordinal)) q.Parameters.AddWithValue("@p",(object?)uid ?? DBNull.Value);
        if (sql.Contains("@room", StringComparison.Ordinal)) q.Parameters.AddWithValue("@room",room);
        try { await q.ExecuteNonQueryAsync(); }
        catch (SqlException exception) { throw new InvalidOperationException($"Disposable fixture batch failed at SQL line {exception.LineNumber}: {sql.Trim()[..Math.Min(100, sql.Trim().Length)]}", exception); }
    }
    public async Task<T> Scalar<T>(string sql, Guid? uid=null)
    {
        await using var c=new SqlConnection(Connection); await c.OpenAsync();
        await using var q=new SqlCommand(sql,c); q.Parameters.AddWithValue("@p",(object?)uid ?? DBNull.Value); return (T)(await q.ExecuteScalarAsync())!;
    }
    public Task<int> AuditCount(Guid uid) => Scalar<int>("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId=CONVERT(nvarchar(100),@p)",uid);
    public Task<string> History(Guid uid) => Scalar<string>("SELECT (SELECT * FROM dbo.ScheduleAppointment WHERE PrimaryResourceId=(SELECT ResourceId FROM dbo.ScheduleResource WHERE ResourceUid=@p) OR RoomResourceId=(SELECT ResourceId FROM dbo.ScheduleResource WHERE ResourceUid=@p) ORDER BY ScheduleAppointmentId FOR JSON PATH) Appointments,(SELECT h.* FROM dbo.AppointmentHistory h JOIN dbo.ScheduleAppointment a ON a.AppointmentUid=h.AppointmentUid JOIN dbo.ScheduleResource r ON r.ResourceId=a.PrimaryResourceId WHERE r.ResourceUid=@p ORDER BY h.AppointmentHistoryId FOR JSON PATH) History,(SELECT * FROM dbo.SchedulingBlockedTime WHERE ResourceUid=@p FOR JSON PATH) Blocks FOR JSON PATH,WITHOUT_ARRAY_WRAPPER",uid);
    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        foreach (var name in created.ToArray())
        {
            if(!Regex.IsMatch(name,@"^MicroEMR_ResourceAdmin_[a-f0-9]{32}$")) throw new InvalidOperationException("Unsafe disposable database name.");
            await using var c=new SqlConnection(Master); await c.OpenAsync();
            await new SqlCommand($"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END",c).ExecuteNonQueryAsync();
            created.Remove(name);
        }
    }
    public sealed class Factory(string connection) : ITenantSqlConnectionFactory
    { public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken=default) { var c=new SqlConnection(connection); await c.OpenAsync(cancellationToken); return c; } }
}
