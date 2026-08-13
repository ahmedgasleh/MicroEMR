using MicroEMR.Application.Tenancy;

namespace MicroEMR.Application.AccessProfiles;

public sealed record BusinessPermission(string Key, string DisplayName, string Group, string Description);

public static class PermissionKeys
{
    public const string PatientsView = "Patients.View";
    public const string PatientsEdit = "Patients.Edit";
}

public static class PermissionCatalog
{
    public static readonly IReadOnlyList<BusinessPermission> All =
    [
        P("Patients.View","View patients","Patients","View patient demographics and charts."), P("Patients.Edit","Edit patients","Patients","Create and update patient demographics."),
        P("Scheduling.View","View scheduling","Scheduling","View schedules and appointments."), P("Scheduling.Manage","Manage scheduling","Scheduling","Create and update appointments and schedules."),
        P("Encounters.View","View encounters","Encounters","View clinical encounters."), P("Encounters.Edit","Edit encounters","Encounters","Create and edit encounters."), P("Encounters.Sign","Sign encounters","Encounters","Finalize and sign encounters."),
        P("Documents.View","View documents","Documents","View patient documents."), P("Documents.Manage","Manage documents","Documents","Create and update patient documents."),
        P("Templates.Use","Use templates","Templates","Use published document templates."), P("Templates.Manage","Manage templates","Templates","Create, publish, and administer templates."),
        P("ClinicalData.Manage","Manage clinical data","Clinical Data","Manage allergies, problems, medications, and vitals."),
        P("Referrals.View","View referrals","Referrals","View patient referrals."), P("Referrals.Manage","Manage referrals","Referrals","Create and update referrals."),
        P("Results.View","View results","Results","View patient results."), P("Results.Review","Review results","Results","Review and acknowledge results."),
        P("Tasks.View","View tasks","Tasks","View patient and clinic tasks."), P("Tasks.Manage","Manage tasks","Tasks","Create and update tasks."),
        P("Reports.View","View reports","Reports","View clinic reports."), P("Reports.Export","Export reports","Reports","Export report data."),
        P("ClinicSettings.Manage","Manage clinic settings","Administration","Change clinic configuration."),
        P("Users.View","View users","Administration","View clinic users."), P("Users.Manage","Manage users","Administration","Add and activate clinic users."), P("Users.ManageAccess","Manage user access","Administration","Configure profiles and user access."),
    ];
    public static readonly IReadOnlySet<string> Keys = All.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
    public static bool IsKnown(string key) => Keys.Contains(key);
    private static BusinessPermission P(string key,string name,string group,string description) => new(key,name,group,description);
}

public sealed record AccessProfileSummary(Guid AccessProfileUid,string Name,string Description,bool IsBuiltIn,bool IsActive,int PermissionCount,int AssignedUserCount,string RowVersion);
public sealed record AccessProfileDetails(Guid AccessProfileUid,string Name,string Description,bool IsBuiltIn,bool IsActive,IReadOnlyCollection<string> PermissionKeys,int AssignedUserCount,string RowVersion);
public sealed record UserAccessProfile(string AuthUserId,Guid? AccessProfileUid,string? AccessProfileName,bool? IsActive,string? RowVersion);
public interface IAccessProfileRepository
{
    Task<IReadOnlyList<AccessProfileSummary>> ListAsync(Guid tenantUid,CancellationToken token=default);
    Task<AccessProfileDetails?> GetAsync(Guid tenantUid,Guid profileUid,CancellationToken token=default);
    Task<IReadOnlyDictionary<string,UserAccessProfile>> GetAssignmentsAsync(Guid tenantUid,IReadOnlyCollection<string> userIds,CancellationToken token=default);
    Task UpdatePermissionsAsync(Guid tenantUid,Guid profileUid,IReadOnlyCollection<string> keys,string rowVersion,string actor,CancellationToken token=default);
    Task AssignAsync(Guid tenantUid,string authUserId,Guid profileUid,string membershipRowVersion,string actor,CancellationToken token=default);
    Task<(string MembershipStatus,IReadOnlyCollection<string> PermissionKeys)> GetEffectiveAsync(Guid tenantUid,string authUserId,CancellationToken token=default);
}
public interface IAccessProfileService
{
    Task<IReadOnlyList<AccessProfileSummary>> ListAsync(CancellationToken token=default);
    Task<AccessProfileDetails?> GetAsync(Guid uid,CancellationToken token=default);
    Task UpdatePermissionsAsync(Guid uid,IReadOnlyCollection<string> keys,string rowVersion,CancellationToken token=default);
    Task AssignAsync(string authUserId,Guid uid,string membershipRowVersion,CancellationToken token=default);
    Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(string authUserId,CancellationToken token=default);
    Task<bool> HasPermissionAsync(string authUserId,string key,CancellationToken token=default);
}
public sealed class AccessProfileService(ITenantContext tenant,IAccessProfileRepository repository,
    TenantUserAdministration.IAuthenticatedSubjectAccessor actor) : IAccessProfileService
{
    public Task<IReadOnlyList<AccessProfileSummary>> ListAsync(CancellationToken token=default)=>repository.ListAsync(tenant.TenantUid,token);
    public Task<AccessProfileDetails?> GetAsync(Guid uid,CancellationToken token=default)=>repository.GetAsync(tenant.TenantUid,uid,token);
    public Task UpdatePermissionsAsync(Guid uid,IReadOnlyCollection<string> keys,string rowVersion,CancellationToken token=default)
    {
        if(keys.Count==0) throw new ArgumentException("Select at least one permission.");
        var normalized=keys.Distinct(StringComparer.Ordinal).Order().ToArray();
        if(normalized.Any(x=>!PermissionCatalog.IsKnown(x))) throw new ArgumentException("One or more permissions are not recognized.");
        return repository.UpdatePermissionsAsync(tenant.TenantUid,uid,normalized,rowVersion,actor.GetRequiredSubject(),token);
    }
    public Task AssignAsync(string user,Guid uid,string version,CancellationToken token=default)=>repository.AssignAsync(tenant.TenantUid,user,uid,version,actor.GetRequiredSubject(),token);
    public async Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(string user,CancellationToken token=default)
    { var x=await repository.GetEffectiveAsync(tenant.TenantUid,user,token); return x.MembershipStatus=="Active"?x.PermissionKeys:[]; }
    public async Task<bool> HasPermissionAsync(string user,string key,CancellationToken token=default)
    { if(!PermissionCatalog.IsKnown(key)) return false; return (await GetEffectivePermissionsAsync(user,token)).Contains(key,StringComparer.Ordinal); }
}
