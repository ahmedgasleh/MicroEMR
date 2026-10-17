using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Tenancy;
using MicroEMR.Application.TenantUserAdministration;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class UserPermissionOverrideTests
{
    [Fact]
    public async Task InheritUsesProfileDenyWinsAndAllowGrants()
    {
        var data=Data("Active",new HashSet<string>{PermissionKeys.SchedulingManage,PermissionKeys.EncountersSign},new Dictionary<string,PermissionOverrideState>
        {
            [PermissionKeys.EncountersSign]=PermissionOverrideState.Deny,
            [PermissionKeys.ReportsExport]=PermissionOverrideState.Allow
        });
        var access=await Service(data).GetUserAccessAsync("user");
        Assert.True(Item(access!,PermissionKeys.SchedulingManage).EffectiveAllowed);
        Assert.False(Item(access!,PermissionKeys.EncountersSign).EffectiveAllowed);
        Assert.True(Item(access!,PermissionKeys.ReportsExport).EffectiveAllowed);
    }

    [Fact]
    public async Task InactiveMembershipDeniesEvenExplicitAllow()
    {
        var access=await Service(Data("Inactive",new HashSet<string>(),new Dictionary<string,PermissionOverrideState>{{PermissionKeys.ReportsExport,PermissionOverrideState.Allow}})).GetUserAccessAsync("user");
        Assert.All(access!.Permissions,x=>Assert.False(x.EffectiveAllowed));
    }

    [Fact]
    public async Task UnknownPermissionIsRejectedBeforePersistence()
    {
        var repo=new Repo(Data("Active",new HashSet<string>(),new Dictionary<string,PermissionOverrideState>()));var service=Service(repo);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SetUserOverrideAsync("user","Unknown.Permission",PermissionOverrideState.Allow,"AAAAAAAAAAA="));
        Assert.Equal(0,repo.SetCalls);
    }

    [Fact]
    public void MigrationIsSequentialTenantScopedAuditedAndConcurrencyProtected()
    {
        var sql=File.ReadAllText(Path.Combine(Root(),"db","platform","012_user_permission_overrides.sql"));
        Assert.Contains("FK_UserPermissionOverride_Membership",sql);Assert.Contains("OverrideState IN ('Allow', 'Deny')",sql);
        Assert.Contains("UserPermissionOverrideChanged",sql);Assert.Contains("@ExpectedMembershipRowVersion",sql);
        Assert.Contains("AccessProfile_GetEffective",sql);Assert.Contains("OverrideState='Deny'",sql);Assert.Contains("OverrideState='Allow'",sql);
    }

    private static UserPermissionAccessItem Item(UserEffectiveAccess x,string key)=>Assert.Single(x.Permissions,p=>p.PermissionKey==key);
    [Fact]
    public async Task EveryPermissionDisplaysExplicitDenyEvenWhenProfileAllowsIt()
    {
        var profile = PermissionCatalog.Keys.ToHashSet(StringComparer.Ordinal);
        var overrides = profile.ToDictionary(key => key, _ => PermissionOverrideState.Deny, StringComparer.Ordinal);
        var access = await Service(Data("Active", profile, overrides)).GetUserAccessAsync("user");

        Assert.Equal(PermissionCatalog.All.Count, access!.Permissions.Count);
        Assert.All(access.Permissions, item =>
        {
            Assert.True(item.ProfileAllowed);
            Assert.Equal(PermissionOverrideState.Deny, item.OverrideState);
            Assert.False(item.EffectiveAllowed);
        });
    }

    [Fact]
    public void AccessReadRepairScopesBothPermissionSourcesBeforeCombiningThem()
    {
        var sql = File.ReadAllText(Path.Combine(Root(), "db", "platform", "026_user_permission_access_scope_repair.sql"));
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.UserPermissionAccess_Get", sql);
        var profile = sql[sql.IndexOf("WITH ProfilePermissions", StringComparison.Ordinal)..sql.IndexOf("UserOverrides AS", StringComparison.Ordinal)];
        Assert.Contains("m.TenantUid=@TenantUid AND m.UserId=@UserId", profile);
        Assert.Contains("p.TenantUid=m.TenantUid AND p.IsActive=1", profile);
        var overrides = sql[sql.IndexOf("UserOverrides AS", StringComparison.Ordinal)..sql.IndexOf("PermissionKeys AS", StringComparison.Ordinal)];
        Assert.Contains("o.TenantUid=@TenantUid AND o.UserId=@UserId", overrides);
        Assert.Contains("m.TenantUid=o.TenantUid AND m.UserId=o.UserId", overrides);
        Assert.Contains("UNION", sql);
        Assert.Contains("LEFT JOIN UserOverrides o ON o.PermissionKey=k.PermissionKey", sql);
        Assert.DoesNotContain("FULL JOIN dbo.UserPermissionOverride", sql);
    }

    private static UserPermissionAccessData Data(string status,IReadOnlySet<string> profile,IReadOnlyDictionary<string,PermissionOverrideState> overrides)=>new(status,Guid.NewGuid(),"Profile","AAAAAAAAAAA=",profile,overrides);
    private static AccessProfileService Service(UserPermissionAccessData data)=>Service(new Repo(data));
    private static AccessProfileService Service(Repo repo)=>new(new TenantContext(Guid.NewGuid(),"t","Tenant"),repo,new Subject());
    private static string Root()=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));
    private sealed class Subject:IAuthenticatedSubjectAccessor{public string GetRequiredSubject()=>"admin";}
    private sealed class Repo(UserPermissionAccessData data):IAccessProfileRepository
    {
        public int SetCalls{get;private set;}public Task<UserPermissionAccessData?>GetUserAccessAsync(Guid t,string u,CancellationToken c=default)=>Task.FromResult<UserPermissionAccessData?>(data);
        public Task SetUserOverrideAsync(Guid t,string u,string k,PermissionOverrideState s,string v,string a,CancellationToken c=default){SetCalls++;return Task.CompletedTask;}
        public Task<(string MembershipStatus,IReadOnlyCollection<string> PermissionKeys)>GetEffectiveAsync(Guid t,string u,CancellationToken c=default)=>Task.FromResult((data.MembershipStatus,(IReadOnlyCollection<string>)[]));
        public Task<IReadOnlyList<AccessProfileSummary>>ListAsync(Guid t,CancellationToken c=default)=>Task.FromResult<IReadOnlyList<AccessProfileSummary>>([]);public Task<AccessProfileDetails?>GetAsync(Guid t,Guid p,CancellationToken c=default)=>Task.FromResult<AccessProfileDetails?>(null);public Task<IReadOnlyDictionary<string,UserAccessProfile>>GetAssignmentsAsync(Guid t,IReadOnlyCollection<string> u,CancellationToken c=default)=>Task.FromResult<IReadOnlyDictionary<string,UserAccessProfile>>(new Dictionary<string,UserAccessProfile>());public Task UpdatePermissionsAsync(Guid t,Guid p,IReadOnlyCollection<string> k,string v,string a,CancellationToken c=default)=>Task.CompletedTask;public Task AssignAsync(Guid t,string u,Guid p,string v,string a,CancellationToken c=default)=>Task.CompletedTask;
    }
}
