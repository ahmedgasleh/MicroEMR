using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PlatformEntitlementProcedureRepairTests
{
    private static readonly string Foundation = Read("018_platform_entitlement_foundation.sql");
    private static readonly string Repair = Read("020_platform_entitlement_procedure_repair.sql");

    [Theory]
    [InlineData("dbo.PlatformEntitlement_AssignToUser")]
    [InlineData("dbo.PlatformEntitlement_RevokeFromUser")]
    public void RepairRecreatesCompleteMigrationEighteenProcedureWithoutSemanticDrift(string procedureName)
    {
        Assert.Equal(Normalize(Procedure(Foundation, procedureName)), Normalize(Procedure(Repair, procedureName)));
    }

    [Theory]
    [InlineData("dbo.PlatformEntitlement_AssignToUser", "PlatformEntitlementAssigned")]
    [InlineData("dbo.PlatformEntitlement_RevokeFromUser", "PlatformEntitlementRevoked")]
    public void RepairedProcedureHasExpectedMetadataConcurrencyAndAuditContract(
        string procedureName, string auditAction)
    {
        var procedure = Procedure(Repair, procedureName);
        Assert.Contains("@UserId NVARCHAR(451)", procedure);
        Assert.Contains("@EntitlementKey NVARCHAR(101)", procedure);
        Assert.Contains("@ActorUserId NVARCHAR(451)", procedure);
        Assert.Contains("@CorrelationId UNIQUEIDENTIFIER", procedure);
        Assert.Contains("DECLARE @LockResource NVARCHAR(100) = CONCAT", procedure);
        Assert.Contains("DECLARE @LockResult INT;", procedure);
        Assert.Contains("EXEC @LockResult = sys.sp_getapplock @Resource = @LockResource", procedure);
        Assert.Contains("SET XACT_ABORT ON", procedure);
        Assert.Contains("BEGIN TRANSACTION", procedure);
        Assert.Contains("COMMIT", procedure);
        Assert.Contains("dbo.PlatformEntitlement", procedure);
        Assert.Contains("dbo.UserPlatformEntitlement", procedure);
        Assert.Contains("dbo.PlatformAuthorizationState", procedure);
        Assert.Contains("INSERT dbo.PlatformAuditEvent\n    (", Normalize(procedure));
        Assert.Contains("PlatformAuditEventUid, ActorUserId, ActorType, Action, TargetTenantUid", procedure);
        Assert.Contains(auditAction, procedure);
        Assert.Equal(1, Count(procedure, "INSERT dbo.PlatformAuditEvent"));
    }

    [Fact]
    public void RepairContainsNoParameterizationArtifactsSchemaMutationDataResetOrGrant()
    {
        Assert.DoesNotMatch(new Regex(@"DECLARE\s+@LockResult\s+(?:AS\s+)?INT\s*=",
            RegexOptions.IgnoreCase), Repair);
        Assert.DoesNotMatch(new Regex(@"@p[a-f0-9]{16,}", RegexOptions.IgnoreCase), Repair);
        Assert.DoesNotContain("CREATE TABLE", Repair, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", Repair, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", Repair, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRUNCATE", Repair, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", Repair, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GRANT ", Repair, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecurityAudit.View", Repair);
        Assert.Equal(2, Count(Repair, "CREATE OR ALTER PROCEDURE"));
    }

    [Fact]
    public void PlatformMigrationTwentyIsUniqueAndTenantSequenceReachesFiftyOne()
    {
        var platformIds = MigrationIds(Path.Combine(Root(), "db", "platform"), 3);
        Assert.Equal(platformIds.Length, platformIds.Distinct().Count());
        Assert.Equal(22, platformIds.Max());
        Assert.Single(platformIds, id => id == 20);

        var tenantIds = MigrationIds(Path.Combine(Root(), "db", "tenant-clinical", "migrations"), 4);
        Assert.Equal(55, tenantIds.Max());
        Assert.Single(tenantIds, id => id == 47);
        Assert.Single(tenantIds, id => id == 48);
    }

    [Fact]
    public void AppliedPlatformMigrationsOneThroughNineteenRemainByteForByteUnchanged()
    {
        var expected = new Dictionary<string, string>
        {
            ["001_create_platform_database.sql"] = "C4160B83F156CE2E502BBC3E19BB0E4C0F83BD1EA1455878E151426B6DD2E264",
            ["002_platform_stored_procedures.sql"] = "4C1F0163338F5853E8F4AE80073584311A4C776F3C5EFD3868F1C9D5E5B00F0D",
            ["003_seed_local_development.sql"] = "E08342B56F1CFBD4EA4003AB4EFC076E190A7E08B8C0B53C9674BFF99343C98B",
            ["004_make_membership_keys_nonclustered.sql"] = "EF19AA08BAE6076E4280E182B96B9BB5DB991081313BF0573A36B30DC4B7849E",
            ["005_seed_local_user_membership.sql"] = "685D7A56463EA7053D11674A2495192D97479EC78C0CE81FF270A9D6AA832F2C",
            ["006_platform_administration.sql"] = "2DFC70153745ABAD6069C8D85F36DBAFB2D6E27368111DEC0595FADFE95EE1E5",
            ["007_membership_activation_lifecycle.sql"] = "945C31A719FACA98A97ED38AC809E8B68AAF66658A69395A696370FD7C5BFBEE",
            ["008_tenant_role_management.sql"] = "383CAD1CD88C99CF1BEEDEC6AE2D01164C80092BB688DB7120730E10B13B0E51",
            ["009_tenant_user_creation.sql"] = "49C9830BA3BAB2FF810A080236EB9FB436E17B781BC29E9DF506E7B8D15FEB12",
            ["010_access_profiles.sql"] = "14B21415A0A7558FBE67920483325E4D78DD3EDC735AF5A35FFCCC658B1DA992",
            ["011_access_profile_assignment_nonclustered_key.sql"] = "5D8A654C2A8FF644F757938A60E00F5008CEC43FFC758CB952830B623358DDC3",
            ["012_user_permission_overrides.sql"] = "A4C4DFE030DFECFA091691DA590F520D9FA934835B841976BE79CF17114F02ED",
            ["013_access_security_stabilization.sql"] = "F8C261CB714C4FE1EB64D9437B07EAB47DF293A392DA112F6ED1BDF23FFB1680",
            ["014_platform_security_denial_audit.sql"] = "08DD728378085F7482FAE51EBF107814206D62972BD11131ACA8AAD3F3F8FF04",
            ["015_platform_cross_patient_security_audit.sql"] = "2EF7E56A721888122477BD23C2B9E8D5FE448C84AE7C5E2CDCBC78CDA31D480D",
            ["016_platform_unresolved_actor_security_audit.sql"] = "ABB584677BFB9BEF64DDE1F1315A52701D7DE8138528996DB916634105DBD421",
            ["017_platform_tenant_security_audit.sql"] = "AF7F8A03CB36F4E6C7B4436FCE8B36BCC06553094D7AE932B39B86E4BB5D7593",
            ["018_platform_entitlement_foundation.sql"] = "59191CC39EACA18C81303B72FFA7A99DB1C728B682612917C3E3A668E211615A",
            ["019_platform_security_audit_review.sql"] = "BA8BEDD2BE3C08A743799EBF93C7F320B926FC93F69C8426E8C56BB3AB8B6A66"
        };

        foreach (var (file, expectedHash) in expected)
            Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(Path.Combine(Root(), "db", "platform", file)))));
    }

    private static string Procedure(string migration, string name)
    {
        var start = migration.IndexOf($"CREATE OR ALTER PROCEDURE {name}", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Procedure {name} was not found.");
        var end = migration.IndexOf("\nGO", start, StringComparison.Ordinal);
        Assert.True(end > start, $"Procedure {name} has no terminating GO batch.");
        return migration[start..end];
    }

    private static int[] MigrationIds(string directory, int digits) => Directory
        .GetFiles(directory, "*.sql")
        .Select(Path.GetFileNameWithoutExtension)
        .Where(name => name?.Length >= digits && int.TryParse(name[..digits], out _))
        .Select(name => int.Parse(name![..digits]))
        .ToArray();

    private static string Normalize(string value) => value.Replace("\r\n", "\n").TrimEnd();
    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;
    private static string Read(string file) =>
        File.ReadAllText(Path.Combine(Root(), "db", "platform", file));
    private static string Root() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
