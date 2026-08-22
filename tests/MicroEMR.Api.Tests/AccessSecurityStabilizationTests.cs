using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class AccessSecurityStabilizationTests
{
    [Theory]
    [InlineData("PatientAllergiesController.cs")]
    [InlineData("PatientMedicationsController.cs")]
    [InlineData("PatientProblemsController.cs")]
    [InlineData("PatientVitalsController.cs")]
    [InlineData("PatientChartAlertsController.cs")]
    public void ClinicalDataControllersRequirePatientView(string file) =>
        Assert.Contains("RequirePermission(PermissionKeys.PatientsView)", Controller(file));

    [Theory]
    [InlineData("PatientAllergiesController.cs")]
    [InlineData("PatientMedicationsController.cs")]
    [InlineData("PatientProblemsController.cs")]
    [InlineData("PatientVitalsController.cs")]
    [InlineData("PatientChartAlertsController.cs")]
    public void ClinicalDataWritesRequireManagePermission(string file) =>
        Assert.Contains("RequirePermission(PermissionKeys.ClinicalDataManage)", Controller(file));

    [Fact]
    public void ReferralDocumentReadsAndWritesUseReferralPermissions()
    {
        var source = Controller("PatientReferralDocumentsController.cs");
        Assert.Contains("RequirePermission(PermissionKeys.ReferralsView)", source);
        Assert.Equal(2, source.Split("RequirePermission(PermissionKeys.ReferralsManage)").Length - 1);
    }

    [Fact]
    public void StabilizationMigrationUsesEffectiveAccessAndSerializesLockoutSensitiveChanges()
    {
        var sql = File.ReadAllText(Path.Combine(Root(), "db", "platform", "013_access_security_stabilization.sql"));
        Assert.Contains("AccessManagementAdministrator", sql);
        Assert.Contains("UserPermissionOverride", sql);
        Assert.Contains("AccessProfilePermission", sql);
        Assert.Contains("sp_getapplock", sql);
        Assert.Contains("PlatformMembership_Deactivate", sql);
        Assert.Contains("AccessProfile_AssignUser", sql);
        Assert.Contains("AccessProfile_ReplacePermissions", sql);
        Assert.Contains("UserPermissionOverride_Set", sql);
        Assert.Equal(4, Count(sql, "DECLARE @LockResource NVARCHAR(255)=CONCAT(N'MicroEMR:AccessAdmin:',@TenantUid);"));
        Assert.Equal(4, Count(sql, "@Resource=@LockResource"));
        Assert.DoesNotContain("@Resource=CONCAT(N'MicroEMR:AccessAdmin:'", sql);
    }

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static string Controller(string file) =>
        File.ReadAllText(Path.Combine(Root(), "src", "MicroEMR.Api", "Controllers", file));

    private static string Root() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
