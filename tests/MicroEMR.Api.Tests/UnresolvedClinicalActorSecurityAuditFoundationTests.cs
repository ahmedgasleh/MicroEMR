using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.SecurityAudit;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class UnresolvedClinicalActorSecurityAuditFoundationTests
{
    private static readonly string Migration = Read(
        "db", "platform", "016_platform_unresolved_actor_security_audit.sql");

    [Fact]
    public void MigrationAddsNoColumnsAndGovernsOnlyApprovedDenialReason()
    {
        foreach (var columnDefinition in new[]
                 {
                     "ADD PatientUid", "ADD RequestedPatientUid", "ADD AuthoritativePatientUid",
                     "ADD ResourceUid", "ADD ResourceType", "ADD ClinicalUserId", "ADD TargetTenantUid",
                     "ADD Metadata", "ADD DetailsJson"
                 })
            Assert.DoesNotContain(columnDefinition, Migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("N'MissingPermission'", Migration);
        Assert.Contains("N'CrossPatientOwnership'", Migration);
        Assert.Contains("N'UnresolvedClinicalActor'", Migration);
        foreach (var futureReason in new[] { "InvalidTenantMembership", "CrossTenantAccess", "InvalidTenantClaim" })
            Assert.DoesNotContain(futureReason, Migration);
    }

    [Fact]
    public void ExistingShapesRemainAndUnresolvedShapeRequiresNullActorTrustedTenantAndNoOwnership()
    {
        Assert.Contains("DenialReason = N'MissingPermission'", Migration);
        Assert.Contains("DenialReason = N'CrossPatientOwnership'", Migration);
        Assert.Contains("RequestedPatientUid <> AuthoritativePatientUid", Migration);
        Assert.Contains("ResourceType = N'Encounter'", Migration);
        Assert.Contains("DenialReason = N'UnresolvedClinicalActor'", Migration);
        Assert.Contains("ClinicalUserId IS NULL", Migration);
        Assert.Contains("TargetTenantUid IS NOT NULL", Migration);
        Assert.Contains("TargetTenantUid <> '00000000-0000-0000-0000-000000000000'", Migration);
        Assert.Contains("Capability = N'EncounterEdit'", Migration);
        Assert.Contains("RequiredPermission = N'Encounters.Edit'", Migration);
        Assert.Contains("SourceApplication = N'MicroEMR.Api'", Migration);
        Assert.True(Count(Migration, "RequestedPatientUid IS NULL") >= 2);
        Assert.True(Count(Migration, "AuthoritativePatientUid IS NULL") >= 2);
        Assert.True(Count(Migration, "ResourceType IS NULL") >= 2);
        Assert.True(Count(Migration, "ResourceUid IS NULL") >= 2);
    }

    [Fact]
    public void CapabilityMatchesApplicationPermissionAndDoesNotExpandMissingPermissionCatalog()
    {
        Assert.Equal("EncounterEdit", SecurityAuditCapabilities.EncounterEdit);
        Assert.Equal("Encounters.Edit", PermissionKeys.EncountersEdit);
        Assert.Contains("Capability = N'EncounterEdit' AND RequiredPermission = N'Encounters.Edit'", Migration);
        Assert.False(SensitiveCapabilityCatalog.TryGetRequiredPermission(
            SecurityAuditCapabilities.EncounterEdit, out _));
    }

    [Fact]
    public void ProcedureAcceptsOnlyIdentityTenantGovernanceAndCorrelation()
    {
        var procedure = Procedure();
        foreach (var parameter in new[]
                 {
                     "@ActorSubject", "@TargetTenantUid", "@Capability", "@RequiredPermission",
                     "@SourceApplication", "@RequestCorrelationId"
                 })
            Assert.Contains(parameter, procedure);

        foreach (var prohibited in new[]
                 {
                     "@ClinicalUserId", "@PatientUid", "@RequestedPatientUid",
                     "@AuthoritativePatientUid", "@ResourceUid", "@ResourceType",
                     "@EventType", "@Outcome", "@DenialReason", "@RequestBody", "@RawUrl"
                 })
        {
            var declaration = procedure[..procedure.IndexOf("AS\n", StringComparison.Ordinal)];
            Assert.DoesNotContain(prohibited, declaration, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ProcedureFixesSemanticsAndInsertsExactlyOneNullActorRow()
    {
        var procedure = Procedure();
        Assert.Contains("N'SecurityAccessDenied'", procedure);
        Assert.Contains("N'Denied'", procedure);
        Assert.Contains("N'UnresolvedClinicalActor'", procedure);
        Assert.Contains("NULL, @TargetTenantUid, N'EncounterEdit', N'Encounters.Edit'", procedure);
        Assert.Contains("NULL, NULL, NULL, NULL", procedure);
        Assert.Contains("SYSUTCDATETIME()", procedure);
        Assert.Equal(1, Count(procedure, "INSERT dbo.PlatformSecurityAuditEvent"));
        Assert.DoesNotContain("UPDATE dbo.PlatformSecurityAuditEvent", procedure, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE dbo.PlatformSecurityAuditEvent", procedure, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("@ActorSubject IS NULL")]
    [InlineData("LEN(LTRIM(RTRIM(@ActorSubject))) = 0")]
    [InlineData("LEN(@ActorSubject) > 450")]
    [InlineData("@TargetTenantUid IS NULL")]
    [InlineData("@Capability <> N'EncounterEdit'")]
    [InlineData("@RequiredPermission <> N'Encounters.Edit'")]
    [InlineData("@SourceApplication <> N'MicroEMR.Api'")]
    [InlineData("LEN(@RequestCorrelationId) > 128")]
    public void ProcedureRejectsMalformedOversizedOrUngovernedInput(string validation) =>
        Assert.Contains(validation, Procedure());

    [Fact]
    public void RepositoryUsesOnlyNarrowStoredProcedureAndPropagatingContract()
    {
        var application = Read("src", "MicroEMR.Application", "SecurityAudit", "PlatformSecurityAudit.cs");
        var repository = Read("src", "MicroEMR.Infrastructure", "SecurityAudit",
            "SqlPlatformSecurityAuditRepository.cs");
        Assert.Contains("UnresolvedClinicalActorSecurityEvent", application);
        Assert.Contains("RecordUnresolvedClinicalActorAsync", application);
        Assert.Contains("dbo.PlatformSecurityAudit_RecordUnresolvedClinicalActor", repository);
        Assert.Contains("CommandType = CommandType.StoredProcedure", repository);
        Assert.DoesNotContain("@ClinicalUserId", Method(repository));
        Assert.DoesNotContain("INSERT", repository, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FoundationDoesNotWireRuntimeOrAlterExistingProceduresAndAdministration()
    {
        var middleware = Read("src", "MicroEMR.Api", "Middleware",
            "ClinicalUserActorResolutionMiddleware.cs");
        Assert.DoesNotContain("RecordUnresolvedClinicalActorAsync", middleware);
        Assert.DoesNotContain("PlatformSecurityAudit", middleware);
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_RecordMissingPermission", Migration);
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_RecordCrossPatientOwnership", Migration);
        Assert.DoesNotContain("ALTER TABLE dbo.PlatformAuditEvent", Migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT dbo.PlatformAuditEvent", Migration);
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PlatformTenant_", Migration);
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PlatformMembership_", Migration);
    }

    [Fact]
    public void MigrationSixteenIsUniqueAndTenantSequenceRemainsAtFortySix()
    {
        var platformIds = Directory.GetFiles(Path.Combine(Root(), "db", "platform"), "*.sql")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name?.Length >= 3 && int.TryParse(name[..3], out _))
            .Select(name => int.Parse(name![..3])).ToArray();
        Assert.Equal(16, platformIds.Max());
        Assert.Single(platformIds, id => id == 16);

        var tenantIds = Directory.GetFiles(Path.Combine(Root(), "db", "tenant-clinical", "migrations"), "*.sql")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name?.Length >= 4 && int.TryParse(name[..4], out _))
            .Select(name => int.Parse(name![..4])).ToArray();
        Assert.Equal(46, tenantIds.Max());
    }

    private static string Procedure() => Migration[Migration.IndexOf(
        "CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_RecordUnresolvedClinicalActor",
        StringComparison.Ordinal)..];

    private static string Method(string source)
    {
        var start = source.IndexOf("RecordUnresolvedClinicalActorAsync", StringComparison.Ordinal);
        var end = source.IndexOf("private static void Add", start, StringComparison.Ordinal);
        return source[start..end];
    }

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static string Root() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
