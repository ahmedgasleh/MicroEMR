namespace MicroEMR.Api.Tests;

using Xunit;

public sealed class PlatformMembershipProfileProvisioningRepairTests
{
    private static readonly string Migration = File.ReadAllText(Path.Combine(
        RepositoryRoot(), "db", "platform", "022_membership_initial_access_profile_resolution.sql"));

    [Fact]
    public void MigrationIsSuccessorAndRepairsProcedureWithoutChangingHistoricalSource()
    {
        var platformFiles = Directory.GetFiles(Path.Combine(RepositoryRoot(), "db", "platform"), "*.sql")
            .Select(Path.GetFileName)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal("022_membership_initial_access_profile_resolution.sql", platformFiles[^1]);
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.PlatformMembership_CreateWithInitialRole", Migration);
        Assert.Contains("DECLARE @ProfileUid UNIQUEIDENTIFIER;", Migration);
        Assert.DoesNotContain("END,@ProfileUid UNIQUEIDENTIFIER", Migration, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ClinicAdministrator", "Clinic Administrator")]
    [InlineData("Physician", "Physician")]
    [InlineData("Nurse", "Nurse")]
    [InlineData("MedicalAssistant", "Medical Assistant")]
    [InlineData("Scheduler", "Reception / Scheduling")]
    public void EverySupportedRoleMapsToItsExistingBuiltInProfile(string role, string profile)
    {
        Assert.Contains($"WHEN N'{role}' THEN N'{profile}'", Migration);
    }

    [Fact]
    public void ProfileUidIsResolvedFromActiveTenantProfile()
    {
        Assert.Contains("SELECT @ProfileUid = AccessProfileUid", Migration);
        Assert.Contains("FROM dbo.AccessProfile WITH (UPDLOCK, HOLDLOCK)", Migration);
        Assert.Contains("WHERE TenantUid = @TenantUid", Migration);
        Assert.Contains("AND Name = @ProfileName", Migration);
        Assert.Contains("AND IsActive = 1", Migration);
        Assert.DoesNotContain("CONVERT(UNIQUEIDENTIFIER", Migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CAST(@ProfileName", Migration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownRoleAndUnavailableProfileFailExplicitly()
    {
        Assert.Contains("ELSE NULL", Migration);
        Assert.Contains("THROW 51310, 'Invalid tenant role.'", Migration);
        Assert.Contains("THROW 51401, 'Default access profile unavailable.'", Migration);
        Assert.DoesNotContain("ELSE N'Clinic Administrator'", Migration);
    }

    [Fact]
    public void MembershipRoleProfileAndAuditRemainAtomicAndDuplicateProtected()
    {
        Assert.Contains("SET XACT_ABORT ON", Migration);
        Assert.Contains("BEGIN TRANSACTION", Migration);
        Assert.Contains("THROW 51301, 'Membership exists.'", Migration);
        Assert.Contains("INSERT dbo.UserTenantMembership", Migration);
        Assert.Contains("INSERT dbo.UserTenantRole", Migration);
        Assert.Contains("INSERT dbo.UserTenantAccessProfile", Migration);
        Assert.Contains("INSERT dbo.PlatformAuditEvent", Migration);
        Assert.Contains("COMMIT", Migration);
    }

    [Fact]
    public void SchedulerPermissionDefinitionRemainsRestricted()
    {
        var permissionSource = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "db", "platform", "021_prescriptions_prescribe_permission_governance.sql"));
        var schedulerDefinition = "(N'Reception / Scheduling',N'Patient demographics and appointment management.',N'Patients.View,Patients.Edit,Scheduling.View,Scheduling.Manage')";

        Assert.Contains(schedulerDefinition, permissionSource);
        Assert.DoesNotContain("ClinicalData.Manage", schedulerDefinition);
        Assert.DoesNotContain("AccessProfilePermission", Migration);
    }

    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
