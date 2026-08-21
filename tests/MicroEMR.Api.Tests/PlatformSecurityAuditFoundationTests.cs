using System.Security.Cryptography;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.SecurityAudit;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PlatformSecurityAuditFoundationTests
{
    private static readonly string Migration = Read(
        "db", "platform", "014_platform_security_denial_audit.sql");

    [Fact]
    public void MigrationCreatesIndependentGovernedSecurityTable()
    {
        Assert.Contains("CREATE TABLE dbo.PlatformSecurityAuditEvent", Migration);
        Assert.Contains("CONSTRAINT PK_PlatformSecurityAuditEvent PRIMARY KEY", Migration);
        Assert.Contains("CHECK (EventType = N'SecurityAccessDenied')", Migration);
        Assert.Contains("CHECK (Outcome = 'Denied')", Migration);
        Assert.Contains("CHECK (DenialReason = N'MissingPermission')", Migration);
        Assert.Contains("ClinicalUserId BIGINT NULL", Migration);
        Assert.Contains("TargetTenantUid UNIQUEIDENTIFIER NULL", Migration);
        Assert.Contains("RequestCorrelationId NVARCHAR(128) NULL", Migration);
        Assert.Contains("OccurredAtUtc DATETIME2(7) NOT NULL", Migration);

        foreach (var forbidden in new[]
                 {
                     "PatientUid", "ResourceUid", "RequestedTenantUid", "IpAddress",
                     "UserAgent", "RawUrl", "QueryString", "RequestBody", "DetailsJson"
                 })
            Assert.DoesNotContain(forbidden, Migration, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(SecurityAuditCapabilities.PatientChartView, PermissionKeys.PatientsView)]
    [InlineData(SecurityAuditCapabilities.EncounterView, PermissionKeys.EncountersView)]
    [InlineData(SecurityAuditCapabilities.PatientDocumentView, PermissionKeys.DocumentsView)]
    [InlineData(SecurityAuditCapabilities.PatientFileDownload, PermissionKeys.DocumentsView)]
    [InlineData(SecurityAuditCapabilities.AppointmentReportRun, PermissionKeys.ReportsView)]
    [InlineData(SecurityAuditCapabilities.AppointmentReportExport, PermissionKeys.ReportsExport)]
    public void SchemaAndProcedureGovernApprovedCapabilityPermissionPairs(
        string capability,
        string permission)
    {
        Assert.Contains(
            $"Capability = N'{capability}' AND RequiredPermission = N'{permission}'",
            Migration);
        Assert.Contains(
            $"@Capability = N'{capability}' AND @RequiredPermission = N'{permission}'",
            Migration);
    }

    [Fact]
    public void ProcedureFixesSemanticsAndInsertsExactlyOneRow()
    {
        var procedure = Migration[Migration.IndexOf(
            "CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_RecordMissingPermission",
            StringComparison.Ordinal)..];

        Assert.DoesNotContain("@EventType", procedure);
        Assert.DoesNotContain("@Outcome", procedure);
        Assert.DoesNotContain("@DenialReason", procedure);
        Assert.Contains("N'SecurityAccessDenied'", procedure);
        Assert.Contains("'Denied'", procedure);
        Assert.Contains("N'MissingPermission'", procedure);
        Assert.Contains("SYSUTCDATETIME()", procedure);
        Assert.Equal(1, Count(procedure, "INSERT dbo.PlatformSecurityAuditEvent"));
        Assert.DoesNotContain("UPDATE dbo.PlatformSecurityAuditEvent", procedure,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", procedure, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcedureRejectsInvalidAndOversizedInputs()
    {
        Assert.Contains("IF @ActorSubject IS NULL", Migration);
        Assert.Contains("LEN(@ActorSubject) > 450", Migration);
        Assert.Contains("@ClinicalUserId <= 0", Migration);
        Assert.Contains("LEN(@Capability) > 100", Migration);
        Assert.Contains("LEN(@RequiredPermission) > 100", Migration);
        Assert.Contains("LEN(@SourceApplication) > 50", Migration);
        Assert.Contains("LEN(@RequestCorrelationId) > 128", Migration);
        Assert.Contains("Source application is not approved.", Migration);
        Assert.Contains("Capability and permission combination is not approved.", Migration);
    }

    [Fact]
    public void InvestigationIndexesAreNarrowAndPurposeSpecific()
    {
        Assert.Contains("IX_PlatformSecurityAuditEvent_OccurredAtUtc", Migration);
        Assert.Contains("IX_PlatformSecurityAuditEvent_TenantTime", Migration);
        Assert.Contains("IX_PlatformSecurityAuditEvent_ActorTime", Migration);
        Assert.Contains("IX_PlatformSecurityAuditEvent_RequestCorrelation", Migration);
    }

    [Fact]
    public void RepositoryUsesStoredProcedureAndIsNotAuthorizationWired()
    {
        var repository = Read("src", "MicroEMR.Infrastructure", "SecurityAudit",
            "SqlPlatformSecurityAuditRepository.cs");
        var authorization = Read("src", "MicroEMR.Api", "Authorization",
            "PermissionAuthorization.cs");

        Assert.Contains("dbo.PlatformSecurityAudit_RecordMissingPermission", repository);
        Assert.Contains("CommandType = CommandType.StoredProcedure", repository);
        Assert.DoesNotContain("INSERT", repository, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IPlatformSecurityAuditRepository", authorization);
        Assert.DoesNotContain("SecurityAudit", authorization);
    }

    [Fact]
    public void ExistingPlatformAuditContractIsNotChangedByMigration()
    {
        Assert.DoesNotContain("ALTER TABLE dbo.PlatformAuditEvent", Migration,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PlatformTenant_", Migration);
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PlatformMembership_", Migration);
        Assert.DoesNotContain("INSERT dbo.PlatformAuditEvent", Migration);
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
            ["015_platform_cross_patient_security_audit.sql"] = "2EF7E56A721888122477BD23C2B9E8D5FE448C84AE7C5E2CDCBC78CDA31D480D"
        };

        foreach (var (file, hash) in expected)
            Assert.Equal(hash, Sha256(ReadBytes("db", "platform", file)));
    }

    [Fact]
    public void PlatformMigrationSequenceEndsAtSixteenWithoutDuplicates()
    {
        var ids = Directory.GetFiles(Path.Combine(Root(), "db", "platform"), "*.sql")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null && name.Length >= 3 && int.TryParse(name[..3], out _))
            .Select(name => int.Parse(name![..3]))
            .OrderBy(id => id)
            .ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Contains(14, ids);
        Assert.Equal(16, ids.Max());
        Assert.Single(ids, id => id == 16);
    }

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static byte[] ReadBytes(params string[] parts) =>
        File.ReadAllBytes(Path.Combine([Root(), .. parts]));

    private static string Sha256(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value));

    private static string Root() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
