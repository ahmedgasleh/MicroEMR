using System.Security.Cryptography;
using MicroEMR.Application.PlatformAdministration;
using MicroEMR.Application.PlatformEntitlements;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PlatformEntitlementFoundationTests
{
    private static readonly string Migration = Read(
        "db", "platform", "018_platform_entitlement_foundation.sql");

    [Fact]
    public void MigrationCreatesMinimalGovernedCatalogAndSeedsOneKey()
    {
        Assert.Contains("CREATE TABLE dbo.PlatformEntitlement", Migration);
        Assert.Contains("PlatformEntitlementUid UNIQUEIDENTIFIER", Migration);
        Assert.Contains("EntitlementKey NVARCHAR(100) COLLATE Latin1_General_100_BIN2 NOT NULL", Migration);
        Assert.Contains("CONSTRAINT UQ_PlatformEntitlement_Key UNIQUE", Migration);
        Assert.Contains("RowVersion ROWVERSION NOT NULL", Migration);
        Assert.Equal(4, Count(Migration, "N'SecurityAudit.View'"));
        Assert.Equal(PlatformEntitlementKeys.SecurityAuditView, "SecurityAudit.View");
        Assert.DoesNotContain("SecurityAudit.Export", Migration);
        Assert.DoesNotContain("PlatformEntitlements.Manage", Migration);
        Assert.Contains("EntitlementKey NOT LIKE N'%*%'", Migration);
    }

    [Fact]
    public void MigrationCreatesHistoricalAssignmentsWithOneActiveConstraint()
    {
        Assert.Contains("CREATE TABLE dbo.UserPlatformEntitlement", Migration);
        Assert.Contains("UserId NVARCHAR(450) NOT NULL", Migration);
        Assert.Contains("AssignedAtUtc DATETIME2(7) NOT NULL", Migration);
        Assert.Contains("AssignedBy NVARCHAR(450) NOT NULL", Migration);
        Assert.Contains("RevokedAtUtc DATETIME2(7) NULL", Migration);
        Assert.Contains("RevokedBy NVARCHAR(450) NULL", Migration);
        Assert.Contains("UX_UserPlatformEntitlement_Active", Migration);
        Assert.Contains("WHERE RevokedAtUtc IS NULL", Migration);
        Assert.DoesNotContain("DELETE dbo.UserPlatformEntitlement", Migration,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClinicalUserId", Migration);
        Assert.DoesNotContain("TenantUid", AssignmentTable());
    }

    [Fact]
    public void AuthorizationStateIsPlatformOwnedMonotonicAndDefaultsToZero()
    {
        Assert.Contains("CREATE TABLE dbo.PlatformAuthorizationState", Migration);
        Assert.Contains("UserId NVARCHAR(450) NOT NULL", Migration);
        Assert.Contains("AuthorizationVersion BIGINT NOT NULL", Migration);
        Assert.Contains("AuthorizationVersion > 0", Migration);
        Assert.Contains("CONVERT(BIGINT, 0)", VersionProcedure());
        Assert.Equal(2, Count(Migration, "AuthorizationVersion = AuthorizationVersion + 1"));
        Assert.DoesNotContain("ALTER TABLE dbo.AspNetUsers", Migration,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActiveReadReturnsOnlyGovernedActiveAssignments()
    {
        var procedure = Procedure("dbo.PlatformEntitlement_GetActiveForUser");
        Assert.Contains("a.RevokedAtUtc IS NULL", procedure);
        Assert.Contains("e.IsActive = 1", procedure);
        Assert.Contains("e.EntitlementKey = N'SecurityAudit.View'", procedure);
        Assert.Contains("SELECT e.EntitlementKey", procedure);
        Assert.DoesNotContain("Tenant", procedure, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AspNetUsers", procedure, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssignmentIsAtomicConcurrencySafeAndAuditedOnce()
    {
        var procedure = Procedure("dbo.PlatformEntitlement_AssignToUser");
        Assert.Contains("SET XACT_ABORT ON", procedure);
        Assert.Contains("BEGIN TRANSACTION", procedure);
        Assert.Contains("sp_getapplock", procedure);
        Assert.Contains("HASHBYTES('SHA2_256'", procedure);
        Assert.Contains("IF @LockResult < 0", procedure);
        Assert.Contains("UPDLOCK, HOLDLOCK", procedure);
        Assert.Contains("The entitlement is already assigned.", procedure);
        Assert.Contains("INSERT dbo.UserPlatformEntitlement", procedure);
        Assert.Contains("AuthorizationVersion = AuthorizationVersion + 1", procedure);
        Assert.Equal(1, Count(procedure, "INSERT dbo.PlatformAuditEvent"));
        Assert.Contains("PlatformEntitlementAssigned", procedure);
        Assert.Contains("TargetTenantUid", procedure);
        Assert.Contains("@UserId, N'Succeeded'", procedure);
        Assert.Contains("COMMIT", procedure);
    }

    [Fact]
    public void RevocationRetainsHistoryIncrementsVersionAndAuditsOnce()
    {
        var procedure = Procedure("dbo.PlatformEntitlement_RevokeFromUser");
        Assert.Contains("sp_getapplock", procedure);
        Assert.Contains("SET RevokedAtUtc = @Now, RevokedBy = @ActorUserId", procedure);
        Assert.DoesNotContain("DELETE", procedure, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AuthorizationVersion = AuthorizationVersion + 1", procedure);
        Assert.Equal(1, Count(procedure, "INSERT dbo.PlatformAuditEvent"));
        Assert.Contains("PlatformEntitlementRevoked", procedure);
        Assert.Contains("The entitlement is not actively assigned.", procedure);
    }

    [Fact]
    public void AdministrativeAuditUsesExistingShapeWithExplicitColumns()
    {
        Assert.DoesNotContain("ALTER TABLE dbo.PlatformAuditEvent", Migration,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, Count(Migration, "INSERT dbo.PlatformAuditEvent\n    ("));
        Assert.Equal(2, Count(Migration, "ActorUserId, ActorType, Action, TargetTenantUid"));
        Assert.Equal(2, Count(Migration, "TargetUserId, Outcome, OccurredAtUtc, CorrelationId, DetailsJson"));
        Assert.DoesNotContain("INSERT dbo.PlatformAuditEvent VALUES", Migration,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PlatformSecurityAuditEvent", Migration);
    }

    [Fact]
    public void RepositoryUsesOnlyGovernedStoredProcedures()
    {
        var repository = Read("src", "MicroEMR.Infrastructure", "PlatformEntitlements",
            "SqlPlatformEntitlementRepository.cs");
        foreach (var procedure in new[]
                 {
                     "dbo.PlatformEntitlement_GetActiveForUser",
                     "dbo.PlatformAuthorization_GetVersionForUser",
                     "dbo.PlatformEntitlement_AssignToUser",
                     "dbo.PlatformEntitlement_RevokeFromUser"
                 })
            Assert.Contains(procedure, repository);
        Assert.Contains("CommandType = CommandType.StoredProcedure", repository);
        Assert.DoesNotContain("INSERT ", repository, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", repository, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", repository, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ServiceRequiresKnownKeyAndExistingIdentity()
    {
        var service = new PlatformEntitlementService(new StubRepository(), new StubIdentityLookup(false));
        Assert.False(PlatformEntitlementKeys.IsKnown("Unknown"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AssignAsync(
            "user", "Unknown", "actor", Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(
            "missing-user", PlatformEntitlementKeys.SecurityAuditView, "actor", Guid.NewGuid()));
    }

    [Fact]
    public async Task ServiceSupportsExplicitAssignmentReadRevokeAndVersion()
    {
        var repository = new StubRepository();
        var service = new PlatformEntitlementService(repository, new StubIdentityLookup(true));
        var assigned = await service.AssignAsync(
            " user-1 ", PlatformEntitlementKeys.SecurityAuditView, " actor ", Guid.NewGuid());
        Assert.Equal(1, assigned.AuthorizationVersion);
        Assert.Equal([PlatformEntitlementKeys.SecurityAuditView], await service.GetActiveForUserAsync("user-1"));
        Assert.Equal(1, await service.GetAuthorizationVersionAsync("user-1"));
        var revoked = await service.RevokeAsync(
            "user-1", PlatformEntitlementKeys.SecurityAuditView, "actor", Guid.NewGuid());
        Assert.Equal(2, revoked.AuthorizationVersion);
        Assert.Empty(await service.GetActiveForUserAsync("user-1"));
    }

    [Fact]
    public void DatabaseToolBootstrapIsExplicitConfirmedAndHasNoRoleFallback()
    {
        var tool = Read("src", "MicroEMR.DatabaseTool", "Program.cs");
        Assert.Contains("group == \"platform-entitlement\"", tool);
        Assert.Contains("action is \"assign\" or \"revoke\"", tool);
        Assert.Contains("Confirm(options, userId)", tool);
        Assert.Contains("Required(options, \"entitlement\")", tool);
        Assert.Contains("IPlatformEntitlementService", tool);
        Assert.DoesNotContain("PlatformRoles", tool);
        Assert.DoesNotContain("SystemAdmin", tool);
    }

    [Fact]
    public void MigrationEighteenIsUniqueAndTenantSequenceRemainsFortySix()
    {
        var platformIds = MigrationIds("db", "platform", 3);
        Assert.Equal(platformIds.Length, platformIds.Distinct().Count());
        Assert.Equal(19, platformIds.Max());
        Assert.Single(platformIds, id => id == 18);
        Assert.Single(platformIds, id => id == 19);
        var tenantIds = MigrationIds("db", "tenant-clinical", "migrations", 4);
        Assert.Equal(46, tenantIds.Max());
        Assert.DoesNotContain(47, tenantIds);
    }

    [Fact]
    public void AppliedPlatformMigrationsRemainByteForByteUnchanged()
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
            ["013_access_security_stabilization.sql"] = "B6B1E60E67281217EAB3C75759C0714053EEFA0F3DCCB57DFC28C425C6139E3D",
            ["014_platform_security_denial_audit.sql"] = "08DD728378085F7482FAE51EBF107814206D62972BD11131ACA8AAD3F3F8FF04",
            ["015_platform_cross_patient_security_audit.sql"] = "2EF7E56A721888122477BD23C2B9E8D5FE448C84AE7C5E2CDCBC78CDA31D480D",
            ["016_platform_unresolved_actor_security_audit.sql"] = "ABB584677BFB9BEF64DDE1F1315A52701D7DE8138528996DB916634105DBD421",
            ["017_platform_tenant_security_audit.sql"] = "AF7F8A03CB36F4E6C7B4436FCE8B36BCC06553094D7AE932B39B86E4BB5D7593"
        };
        foreach (var (file, expectedHash) in expected)
            Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(Path.Combine(Root(), "db", "platform", file)))));
    }

    private static string AssignmentTable() => Migration[Migration.IndexOf(
        "CREATE TABLE dbo.UserPlatformEntitlement", StringComparison.Ordinal)..Migration.IndexOf(
        "CREATE UNIQUE INDEX UX_UserPlatformEntitlement_Active", StringComparison.Ordinal)];

    private static string VersionProcedure() => Procedure("dbo.PlatformAuthorization_GetVersionForUser");

    private static string Procedure(string name)
    {
        var start = Migration.IndexOf($"CREATE OR ALTER PROCEDURE {name}", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = Migration.IndexOf("\nGO", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return Migration[start..end];
    }

    private static int[] MigrationIds(string first, string second, int digits) =>
        MigrationIds([first, second], digits);

    private static int[] MigrationIds(string first, string second, string third, int digits) =>
        MigrationIds([first, second, third], digits);

    private static int[] MigrationIds(string[] parts, int digits) => Directory
        .GetFiles(Path.Combine([Root(), .. parts]), "*.sql")
        .Select(Path.GetFileNameWithoutExtension)
        .Where(name => name?.Length >= digits && int.TryParse(name[..digits], out _))
        .Select(name => int.Parse(name![..digits]))
        .ToArray();

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static string Root() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private sealed class StubIdentityLookup(bool exists) : IIdentityUserLookup
    {
        public bool IsAvailable => true;
        public Task<bool> ExistsAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(exists);
    }

    private sealed class StubRepository : IPlatformEntitlementRepository
    {
        private bool _active;
        private long _version;
        public Task<IReadOnlyList<string>> GetActiveForUserAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(_active ? [PlatformEntitlementKeys.SecurityAuditView] : []);
        public Task<long> GetAuthorizationVersionAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_version);
        public Task<PlatformEntitlementChangeResult> AssignAsync(string userId, string entitlementKey,
            string actorUserId, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _active = true;
            return Task.FromResult(new PlatformEntitlementChangeResult(Guid.NewGuid(), ++_version));
        }
        public Task<PlatformEntitlementChangeResult> RevokeAsync(string userId, string entitlementKey,
            string actorUserId, Guid correlationId, CancellationToken cancellationToken = default)
        {
            _active = false;
            return Task.FromResult(new PlatformEntitlementChangeResult(Guid.NewGuid(), ++_version));
        }
    }
}
