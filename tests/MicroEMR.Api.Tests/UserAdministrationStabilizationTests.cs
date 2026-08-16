using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.PlatformAdministration;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Authorization;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class UserAdministrationStabilizationTests
{
    [Fact]
    public void ApiOperationsUseGranularEffectiveUserPermissions()
    {
        var authorize = typeof(TenantUserAdministrationController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>();
        Assert.Contains(authorize, x => x.Policy is null);
        AssertPermission(nameof(TenantUserAdministrationController.Get), PermissionKeys.UsersView);
        AssertPermission(nameof(TenantUserAdministrationController.GetUser), PermissionKeys.UsersView);
        AssertPermission(nameof(TenantUserAdministrationController.AddUser), PermissionKeys.UsersManage);
        AssertPermission(nameof(TenantUserAdministrationController.UpdateRoles), PermissionKeys.UsersManageAccess);
        AssertPermission(nameof(TenantUserAdministrationController.ResetPassword), PermissionKeys.UsersManageAccess);
        Assert.Equal(TenantRoleCatalog.ClinicAdministrator, ClinicConfigurationAuthorization.Role);
    }

    private static void AssertPermission(string action, string permission) =>
        Assert.Contains(typeof(TenantUserAdministrationController).GetMethod(action)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(),
            x => x.Policy == PermissionPolicyProvider.Prefix + permission);

    [Fact]
    public void MutationContractsRejectOverPostingByConstruction()
    {
        Assert.Equal(["RowVersion"], typeof(MembershipRowVersionRequest).GetProperties().Select(x => x.Name).ToArray());
        Assert.Equal(["SelectedRoles", "RowVersion"], typeof(TenantRoleUpdateRequest).GetProperties().Select(x => x.Name).ToArray());
        var provision = typeof(TenantUserAdministrationController).GetMethod(nameof(TenantUserAdministrationController.ProvisionClinicalUser))!;
        Assert.Equal(["authUserId", "cancellationToken"], provision.GetParameters().Select(x => x.Name!).ToArray());
        Assert.DoesNotContain(provision.GetParameters(), x => x.ParameterType.IsClass && x.ParameterType != typeof(string));
    }

    [Fact]
    public void AdminPageUsesCorrectCellsAndContainsOnlyCompletedActions()
    {
        var view = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "MicroEMR.Web", "Views",
            "TenantUserAdministration", "Index.cshtml"));
        Assert.Contains("<td data-tenant-roles>", view);
        Assert.DoesNotContain("<td data-tenant-roles>\r\n                                <div class=\"fw-semibold\">", view);
        Assert.Contains("Last active administrator", view);
        Assert.Contains("Provision Clinical User", view);
        Assert.DoesNotContain("Unprovision", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Provider Link", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password Reset", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invite", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Delete User", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("type=\"text\" name=\"tenantRole\"", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlatformMutationsAreTenantScopedConcurrentAndAtomic()
    {
        var lifecycle = File.ReadAllText(Path.Combine(RepositoryRoot(), "db", "platform", "007_membership_activation_lifecycle.sql"));
        var roles = File.ReadAllText(Path.Combine(RepositoryRoot(), "db", "platform", "008_tenant_role_management.sql"));
        foreach (var sql in new[] { lifecycle, roles })
        {
            Assert.Contains("TenantUid=@TenantUid", sql);
            Assert.Contains("UPDLOCK,HOLDLOCK", sql);
            Assert.Contains("@ExpectedRowVersion", sql);
            Assert.Contains("BEGIN TRANSACTION", sql);
        }
        Assert.Contains("MembershipStatus='Active'", roles);
        Assert.Contains("RoleName=N'ClinicAdministrator'", roles);
        Assert.Contains("RoleName=N'ClinicAdministrator'", lifecycle);
    }

    [Fact]
    public void ProvisioningRemainsExactIdempotentAndTenantConnected()
    {
        var mapping = File.ReadAllText(Path.Combine(RepositoryRoot(), "db", "tenant-clinical", "migrations",
            "0018-clinical-user-auth-subject.sql"));
        var provisioning = File.ReadAllText(Path.Combine(RepositoryRoot(), "db", "tenant-clinical", "migrations",
            "0019-clinical-user-provisioning.sql"));
        Assert.Contains("Latin1_General_100_BIN2", mapping);
        Assert.Contains("CREATE UNIQUE INDEX UX_ApplicationUser_AuthSubjectId", mapping);
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK)", provisioning);
        Assert.Contains("EXEC dbo.ApplicationUser_GetByAuthSubjectId", provisioning);
        Assert.Contains("explicit mapping is required", provisioning);
        Assert.DoesNotContain("TRY_CONVERT(BIGINT", provisioning, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", ".."));
}
