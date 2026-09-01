using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PlatformAccessManagementAdministratorRepairTests
{
    private static string Migration() => File.ReadAllText(Path.Combine(
        Root(), "db", "platform", "024_access_management_administrator_repair.sql"));

    [Fact]
    public void RepairRestoresCanonicalEffectiveAdministratorFunction()
    {
        var sql = Migration();

        Assert.Contains("CREATE OR ALTER FUNCTION dbo.AccessManagementAdministrator", sql);
        Assert.Contains("m.MembershipStatus=N'Active'", sql);
        Assert.Contains("o.OverrideState='Allow'", sql);
        Assert.Contains("o.OverrideState='Deny'", sql);
        Assert.Contains("pp.PermissionKey=N'Users.ManageAccess'", sql);
        Assert.Contains("p.TenantUid=m.TenantUid AND p.IsActive=1", sql);
    }

    [Fact]
    public void RepairFailsClosedUnlessProviderPermissionProceduresAreCurrent()
    {
        var sql = Migration();

        Assert.Contains("Access-management repair prerequisites are missing", sql);
        Assert.Contains("AccessProfile_ReplacePermissions", sql);
        Assert.Contains("UserPermissionOverride_Set", sql);
        Assert.Contains("OBJECT_DEFINITION", sql);
        Assert.Contains("Providers.Manage", sql);
        Assert.Contains("OBJECT_ID(N'dbo.AccessManagementAdministrator', N'IF')", sql);
    }

    [Fact]
    public void RepairIsForwardOnlyAndDoesNotReplayHistoricalMigrations()
    {
        var sql = Migration();

        Assert.DoesNotContain(":r", sql);
        Assert.DoesNotContain("013_access_security_stabilization.sql", sql);
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE", sql);
    }

    private static string Root() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
