using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Tenancy;
using MicroEMR.Application.TenantUserAdministration;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PermissionEnforcementTests
{
    [Fact]
    public async Task CurrentUserPermissionsAreLoadedOncePerRequestScope()
    {
        var repository = new Repository("Active", [PermissionKeys.PatientsView]);
        var tenant = new TenantContextAccessor();
        tenant.SetTenant(new TenantContext(Guid.NewGuid(), "clinic-a", "Clinic A"));
        var service = new CurrentUserPermissionService(
            tenant, repository, new Subject("user-1"));

        Assert.True(await service.HasPermissionAsync(PermissionKeys.PatientsView));
        Assert.False(await service.HasPermissionAsync(PermissionKeys.EncountersSign));
        Assert.Equal(1, repository.EffectiveCalls);
    }

    [Fact]
    public async Task InactiveMembershipIsThePermissionMasterSwitch()
    {
        var tenant = new TenantContextAccessor();
        tenant.SetTenant(new TenantContext(Guid.NewGuid(), "clinic-a", "Clinic A"));
        var service = new CurrentUserPermissionService(
            tenant,
            new Repository("Inactive", [PermissionKeys.UsersManageAccess]), new Subject("user-1"));

        Assert.Empty(await service.GetEffectivePermissionsAsync());
    }

    [Fact]
    public async Task PermissionServiceCanBeConstructedBeforeTenantResolution()
    {
        var tenant = new TenantContextAccessor();
        var repository = new Repository("Active", [PermissionKeys.PatientsView]);
        var service = new CurrentUserPermissionService(tenant, repository, new Subject("user-1"));

        tenant.SetTenant(new TenantContext(Guid.NewGuid(), "clinic-a", "Clinic A"));

        Assert.True(await service.HasPermissionAsync(PermissionKeys.PatientsView));
    }

    [Theory]
    [InlineData(typeof(PatientEncountersController), PermissionKeys.EncountersView)]
    [InlineData(typeof(PatientDocumentsController), PermissionKeys.DocumentsView)]
    [InlineData(typeof(SchedulingController), PermissionKeys.SchedulingView)]
    [InlineData(typeof(AppointmentReportsController), PermissionKeys.ReportsView)]
    public void MajorApiAreasDeclareBusinessPermission(Type controller, string permission)
    {
        var policies = controller.GetCustomAttributes(true).OfType<AuthorizeAttribute>()
            .Select(x => x.Policy).Where(x => x is not null);
        Assert.Contains(PermissionPolicyProvider.Prefix + permission, policies);
    }

    [Fact]
    public void HighRiskActionsHaveSpecificPermissions()
    {
        AssertActionPermission<PatientEncountersController>("SignEncounter", PermissionKeys.EncountersSign);
        AssertActionPermission<AppointmentReportsController>("Csv", PermissionKeys.ReportsExport);
        AssertActionPermission<TenantUserAdministrationController>("UpdateRoles", PermissionKeys.UsersManageAccess);
    }

    private static void AssertActionPermission<T>(string method, string key) =>
        Assert.Contains(typeof(T).GetMethod(method)!.GetCustomAttributes(true).OfType<AuthorizeAttribute>(),
            x => x.Policy == PermissionPolicyProvider.Prefix + key);

    private sealed record Subject(string Value) : IAuthenticatedSubjectAccessor
    {
        public string GetRequiredSubject() => Value;
    }

    private sealed class Repository(string status, IReadOnlyCollection<string> keys) : IAccessProfileRepository
    {
        public int EffectiveCalls { get; private set; }
        public Task<(string MembershipStatus, IReadOnlyCollection<string> PermissionKeys)> GetEffectiveAsync(Guid tenantUid, string authUserId, CancellationToken token = default)
        { EffectiveCalls++; return Task.FromResult((status, keys)); }
        public Task<IReadOnlyList<AccessProfileSummary>> ListAsync(Guid tenantUid, CancellationToken token = default) => Task.FromResult<IReadOnlyList<AccessProfileSummary>>([]);
        public Task<AccessProfileDetails?> GetAsync(Guid tenantUid, Guid profileUid, CancellationToken token = default) => Task.FromResult<AccessProfileDetails?>(null);
        public Task<IReadOnlyDictionary<string, UserAccessProfile>> GetAssignmentsAsync(Guid tenantUid, IReadOnlyCollection<string> userIds, CancellationToken token = default) => Task.FromResult<IReadOnlyDictionary<string, UserAccessProfile>>(new Dictionary<string, UserAccessProfile>());
        public Task UpdatePermissionsAsync(Guid tenantUid, Guid profileUid, IReadOnlyCollection<string> permissionKeys, string rowVersion, string actor, CancellationToken token = default) => Task.CompletedTask;
        public Task AssignAsync(Guid tenantUid, string authUserId, Guid profileUid, string membershipRowVersion, string actor, CancellationToken token = default) => Task.CompletedTask;
    }
}
