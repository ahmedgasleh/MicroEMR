using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.SecurityAudit;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class CrossPatientSecurityAuditFoundationTests
{
    private static readonly string Migration = Read(
        "db", "platform", "015_platform_cross_patient_security_audit.sql");

    [Fact]
    public void MigrationAddsOnlyNullableOwnershipIdentityColumns()
    {
        Assert.Contains("ADD RequestedPatientUid UNIQUEIDENTIFIER NULL", Migration);
        Assert.Contains("ADD AuthoritativePatientUid UNIQUEIDENTIFIER NULL", Migration);
        Assert.Contains("ADD ResourceType NVARCHAR(50) NULL", Migration);
        Assert.Contains("ADD ResourceUid UNIQUEIDENTIFIER NULL", Migration);

        foreach (var prohibited in new[]
                 {
                     "PatientName", "EncounterText", "AddendumText", "DocumentTitle",
                     "FileName", "HealthCard", "ClinicalSummary", "RequestBody", "RawUrl", "QueryString"
                 })
            Assert.DoesNotContain(prohibited, Migration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DenialReasonAndShapeConstraintsPreserveMissingPermissionAndGovernOwnership()
    {
        Assert.Contains("DenialReason IN (N'MissingPermission', N'CrossPatientOwnership')", Migration);
        Assert.Contains("DenialReason = N'MissingPermission'", Migration);
        Assert.Contains("RequestedPatientUid IS NULL", Migration);
        Assert.Contains("AuthoritativePatientUid IS NULL", Migration);
        Assert.Contains("ResourceType IS NULL", Migration);
        Assert.Contains("ResourceUid IS NULL", Migration);
        Assert.Contains("DenialReason = N'CrossPatientOwnership'", Migration);
        Assert.Contains("TargetTenantUid IS NOT NULL", Migration);
        Assert.Contains("RequestedPatientUid IS NOT NULL", Migration);
        Assert.Contains("AuthoritativePatientUid IS NOT NULL", Migration);
        Assert.Contains("RequestedPatientUid <> AuthoritativePatientUid", Migration);
        Assert.Contains("ResourceUid IS NOT NULL", Migration);
    }

    [Fact]
    public void InitialOwnershipContractAllowsOnlyEncounterViewEncounterApiMapping()
    {
        Assert.Contains("Capability = N'EncounterView'", Migration);
        Assert.Contains("RequiredPermission = N'Encounters.View'", Migration);
        Assert.Contains("ResourceType = N'Encounter'", Migration);
        Assert.Contains("SourceApplication = N'MicroEMR.Api'", Migration);
        Assert.DoesNotContain("ResourceType = N'PatientFile'", Migration);
        Assert.DoesNotContain("ResourceType = N'PatientDocument'", Migration);
        Assert.DoesNotContain("ResourceType = N'Referral'", Migration);
        Assert.Equal("Encounter", SecurityAuditResourceTypes.Encounter);
        Assert.Equal("EncounterView", SecurityAuditCapabilities.EncounterView);
        Assert.Equal("Encounters.View", PermissionKeys.EncountersView);
    }

    [Fact]
    public void ProcedureFixesSemanticsAndInsertsExactlyOnce()
    {
        var procedure = Procedure();
        Assert.DoesNotContain("@EventType", procedure);
        Assert.DoesNotContain("@Outcome", procedure);
        Assert.DoesNotContain("@DenialReason", procedure);
        Assert.Contains("N'SecurityAccessDenied'", procedure);
        Assert.Contains("'Denied'", procedure);
        Assert.Contains("N'CrossPatientOwnership'", procedure);
        Assert.Contains("N'EncounterView'", procedure);
        Assert.Contains("N'Encounters.View'", procedure);
        Assert.Contains("N'Encounter'", procedure);
        Assert.Contains("SYSUTCDATETIME()", procedure);
        Assert.Equal(1, Count(procedure, "INSERT dbo.PlatformSecurityAuditEvent"));
        Assert.DoesNotContain("UPDATE dbo.PlatformSecurityAuditEvent", procedure,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", procedure, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("@ActorSubject IS NULL")]
    [InlineData("@ClinicalUserId <= 0")]
    [InlineData("@TargetTenantUid IS NULL")]
    [InlineData("@RequestedPatientUid IS NULL")]
    [InlineData("@AuthoritativePatientUid IS NULL")]
    [InlineData("@RequestedPatientUid = @AuthoritativePatientUid")]
    [InlineData("@ResourceUid IS NULL")]
    [InlineData("@Capability <> N'EncounterView'")]
    [InlineData("@ResourceType <> N'Encounter'")]
    [InlineData("@SourceApplication <> N'MicroEMR.Api'")]
    [InlineData("LEN(@RequestCorrelationId) > 128")]
    public void ProcedureRejectsMalformedOrUngovernedOwnershipEvents(string validation) =>
        Assert.Contains(validation, Procedure());

    [Fact]
    public void ProcedureAcceptsNullableClinicalUserAndStoresAllTrustedIdentifiers()
    {
        var procedure = Procedure();
        Assert.Contains("@ClinicalUserId BIGINT = NULL", procedure);
        Assert.Contains("@TargetTenantUid UNIQUEIDENTIFIER", procedure);
        Assert.Contains("@RequestedPatientUid UNIQUEIDENTIFIER", procedure);
        Assert.Contains("@AuthoritativePatientUid UNIQUEIDENTIFIER", procedure);
        Assert.Contains("@ResourceUid UNIQUEIDENTIFIER", procedure);
        Assert.Contains("@RequestCorrelationId NVARCHAR(129) = NULL", procedure);
        Assert.Contains("@ActorSubject", procedure);
        Assert.Contains("@RequestCorrelationId", procedure);
    }

    [Fact]
    public void InvestigationIndexTargetsResolvedOwnershipResourceWithoutRedundantTenantIndex()
    {
        Assert.Contains("IX_PlatformSecurityAuditEvent_OwnershipResourceTime", Migration);
        Assert.Contains("TargetTenantUid", Migration);
        Assert.Contains("ResourceType", Migration);
        Assert.Contains("ResourceUid", Migration);
        Assert.Contains("OccurredAtUtc DESC", Migration);
        Assert.Contains("WHERE DenialReason = N'CrossPatientOwnership'", Migration);
        Assert.DoesNotContain("IX_PlatformSecurityAuditEvent_TenantTime", Migration);
    }

    [Fact]
    public void RepositoryUsesNarrowProcedureOnlyAndDoesNotInsertDirectly()
    {
        var repository = Read("src", "MicroEMR.Infrastructure", "SecurityAudit",
            "SqlPlatformSecurityAuditRepository.cs");
        Assert.Contains("RecordCrossPatientOwnershipAsync", repository);
        Assert.Contains("dbo.PlatformSecurityAudit_RecordCrossPatientOwnership", repository);
        Assert.Contains("CommandType = CommandType.StoredProcedure", repository);
        Assert.Contains("@RequestedPatientUid", repository);
        Assert.Contains("@AuthoritativePatientUid", repository);
        Assert.Contains("@ResourceUid", repository);
        Assert.DoesNotContain("INSERT", repository, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeOwnershipAuditDoesNotLeakIntoEncounterApplicationService()
    {
        var service = Read("src", "MicroEMR.Application", "PatientEncounters", "Services",
            "PatientEncounterService.cs");
        Assert.DoesNotContain("RecordCrossPatientOwnershipAsync", service);
    }

    [Fact]
    public void MigrationDoesNotRedefineMissingPermissionOrAdministrativeProcedures()
    {
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_RecordMissingPermission", Migration);
        Assert.DoesNotContain("ALTER TABLE dbo.PlatformAuditEvent", Migration,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT dbo.PlatformAuditEvent", Migration);
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PlatformTenant_", Migration);
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PlatformMembership_", Migration);
    }

    [Fact]
    public void PlatformMigrationFifteenRemainsUniqueAndTenantMigrationsRemainAtFortySix()
    {
        var platformIds = Directory.GetFiles(Path.Combine(Root(), "db", "platform"), "*.sql")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name?.Length >= 3 && int.TryParse(name[..3], out _))
            .Select(name => int.Parse(name![..3]))
            .ToArray();
        Assert.Single(platformIds, id => id == 15);
        Assert.Single(platformIds, id => id == 16);

        var tenantFiles = Directory.GetFiles(Path.Combine(Root(), "db", "tenant-clinical", "migrations"), "*.sql");
        Assert.DoesNotContain(tenantFiles, file => Path.GetFileName(file).StartsWith("0047", StringComparison.Ordinal));
        Assert.Contains(tenantFiles, file => Path.GetFileName(file).StartsWith("0046", StringComparison.Ordinal));
    }

    private static string Procedure() => Migration[Migration.IndexOf(
        "CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_RecordCrossPatientOwnership",
        StringComparison.Ordinal)..];

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static string Root() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
