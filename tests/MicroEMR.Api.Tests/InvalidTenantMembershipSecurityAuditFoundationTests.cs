using MicroEMR.Application.SecurityAudit;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class InvalidTenantMembershipSecurityAuditFoundationTests
{
    private static readonly string Migration = Read(
        "db", "platform", "017_platform_tenant_security_audit.sql");

    [Fact]
    public void MigrationAddsOnlyRequestedTenantAndMakesPermissionNullable()
    {
        Assert.Contains("ADD RequestedTenantUid UNIQUEIDENTIFIER NULL", Migration);
        Assert.Contains("ALTER COLUMN RequiredPermission NVARCHAR(100) NULL", Migration);

        foreach (var prohibited in new[]
                 {
                     "RequestedTenantKey", "TenantName", "MembershipStatus", "ClinicalContent",
                     "RequestBody", "RawUrl", "QueryString", "CrossTenantAccess"
                 })
            Assert.DoesNotContain(prohibited, Migration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DenialAndCapabilityGovernanceAddsOnlyInvalidMembershipSelection()
    {
        foreach (var reason in new[]
                 {
                     "N'MissingPermission'", "N'CrossPatientOwnership'",
                     "N'UnresolvedClinicalActor'", "N'InvalidTenantMembership'"
                 })
            Assert.Contains(reason, Migration);

        Assert.Contains("RequiredPermission IS NOT NULL", Migration);
        Assert.Contains("Capability = N'TenantSelection' AND RequiredPermission IS NULL", Migration);
        Assert.DoesNotContain("TenantAccess", Migration);
        Assert.DoesNotContain("ClinicalApplicationAccess", Migration);
        Assert.Equal("TenantSelection", SecurityAuditCapabilities.TenantSelection);
        Assert.Equal("MicroEMR.Auth", SecurityAuditSourceApplications.Auth);
    }

    [Fact]
    public void ExistingShapesRequireNoRequestedTenantAndRetainPermissions()
    {
        var missing = Shape("DenialReason = N'MissingPermission'", "DenialReason = N'CrossPatientOwnership'");
        Assert.Contains("RequiredPermission IS NOT NULL", missing);
        Assert.Contains("RequestedTenantUid IS NULL", missing);

        var ownership = Shape("DenialReason = N'CrossPatientOwnership'", "DenialReason = N'UnresolvedClinicalActor'");
        Assert.Contains("RequiredPermission = N'Encounters.View'", ownership);
        Assert.Contains("RequestedTenantUid IS NULL", ownership);
        Assert.Contains("RequestedPatientUid <> AuthoritativePatientUid", ownership);

        var unresolved = Shape("DenialReason = N'UnresolvedClinicalActor'", "DenialReason = N'InvalidTenantMembership'");
        Assert.Contains("RequiredPermission = N'Encounters.Edit'", unresolved);
        Assert.Contains("RequestedTenantUid IS NULL", unresolved);
        Assert.Contains("TargetTenantUid IS NOT NULL", unresolved);
    }

    [Fact]
    public void InvalidMembershipShapeSeparatesRequestedFromTrustedTenant()
    {
        var shape = Shape("DenialReason = N'InvalidTenantMembership'", "\n        );");
        Assert.Contains("ClinicalUserId IS NULL", shape);
        Assert.Contains("TargetTenantUid IS NULL", shape);
        Assert.Contains("RequestedTenantUid IS NOT NULL", shape);
        Assert.Contains("RequestedTenantUid <> '00000000-0000-0000-0000-000000000000'", shape);
        Assert.Contains("Capability = N'TenantSelection'", shape);
        Assert.Contains("RequiredPermission IS NULL", shape);
        Assert.Contains("SourceApplication = N'MicroEMR.Auth'", shape);
        Assert.Contains("RequestedPatientUid IS NULL", shape);
        Assert.Contains("AuthoritativePatientUid IS NULL", shape);
        Assert.Contains("ResourceType IS NULL", shape);
        Assert.Contains("ResourceUid IS NULL", shape);
    }

    [Fact]
    public void AuthSourceIsPermittedOnlyForInvalidMembership()
    {
        Assert.Contains("DenialReason <> N'InvalidTenantMembership'", Migration);
        Assert.Contains("SourceApplication IN (N'MicroEMR.Api', N'MicroEMR.Web')", Migration);
        Assert.Contains("DenialReason = N'InvalidTenantMembership'", Migration);
        Assert.Contains("SourceApplication = N'MicroEMR.Auth'", Migration);
    }

    [Fact]
    public void ProcedureAcceptsOnlySubjectRequestedTenantSourceAndCorrelation()
    {
        var declaration = Procedure()[..Procedure().IndexOf("\nAS", StringComparison.Ordinal)];
        foreach (var parameter in new[]
                 {
                     "@ActorSubject", "@RequestedTenantUid", "@SourceApplication", "@RequestCorrelationId"
                 })
            Assert.Contains(parameter, declaration);

        foreach (var prohibited in new[]
                 {
                     "@TargetTenantUid", "@ClinicalUserId", "@RequiredPermission", "@Capability",
                     "@PatientUid", "@RequestedPatientUid", "@ResourceUid", "@DenialReason",
                     "@EventType", "@Outcome"
                 })
            Assert.DoesNotContain(prohibited, declaration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcedureFixesSemanticsAndInsertsExactlyOneNarrowRow()
    {
        var procedure = Procedure();
        Assert.Contains("N'SecurityAccessDenied'", procedure);
        Assert.Contains("N'Denied'", procedure);
        Assert.Contains("N'InvalidTenantMembership'", procedure);
        Assert.Contains("NULL, NULL, @RequestedTenantUid, N'TenantSelection'", procedure);
        Assert.Contains("NULL, @SourceApplication, @RequestCorrelationId, SYSUTCDATETIME()", procedure);
        Assert.Contains("NULL, NULL, NULL, NULL", procedure);
        Assert.Equal(1, Count(procedure, "INSERT dbo.PlatformSecurityAuditEvent"));
        Assert.DoesNotContain("UPDATE dbo.PlatformSecurityAuditEvent", procedure, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE dbo.PlatformSecurityAuditEvent", procedure, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TenantDatabase", procedure, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApplicationUser", procedure, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("@ActorSubject IS NULL")]
    [InlineData("LEN(LTRIM(RTRIM(@ActorSubject))) = 0")]
    [InlineData("LEN(@ActorSubject) > 450")]
    [InlineData("@RequestedTenantUid IS NULL")]
    [InlineData("@RequestedTenantUid = '00000000-0000-0000-0000-000000000000'")]
    [InlineData("@SourceApplication <> N'MicroEMR.Auth'")]
    [InlineData("LEN(@SourceApplication) > 50")]
    [InlineData("LEN(@RequestCorrelationId) > 128")]
    public void ProcedureRejectsMalformedOversizedOrUngovernedInput(string validation) =>
        Assert.Contains(validation, Procedure());

    [Fact]
    public void RepositoryUsesOnlyNarrowPlatformProcedureAndRuntimeIsNotWired()
    {
        var application = Read("src", "MicroEMR.Application", "SecurityAudit", "PlatformSecurityAudit.cs");
        var repository = Read("src", "MicroEMR.Infrastructure", "SecurityAudit",
            "SqlPlatformSecurityAuditRepository.cs");
        var selectionController = Read("src", "MicroEMR.Auth", "Controllers", "AccountController.cs");

        Assert.Contains("InvalidTenantMembershipSecurityEvent", application);
        Assert.Contains("RecordInvalidTenantMembershipAsync", application);
        Assert.Contains("dbo.PlatformSecurityAudit_RecordInvalidTenantMembership", repository);
        Assert.Contains("@RequestedTenantUid", repository);
        Assert.Contains("CommandType = CommandType.StoredProcedure", repository);
        Assert.DoesNotContain("INSERT", repository, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RecordInvalidTenantMembershipAsync", selectionController);
        Assert.DoesNotContain("InvalidTenantMembershipSecurityEvent", selectionController);
    }

    [Fact]
    public void MigrationDoesNotAlterAdministrativeAuditOrExistingProcedures()
    {
        Assert.DoesNotContain("ALTER TABLE dbo.PlatformAuditEvent", Migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT dbo.PlatformAuditEvent", Migration);
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_RecordMissingPermission", Migration);
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_RecordCrossPatientOwnership", Migration);
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_RecordUnresolvedClinicalActor", Migration);
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PlatformTenant_", Migration);
        Assert.DoesNotContain("CREATE OR ALTER PROCEDURE dbo.PlatformMembership_", Migration);
    }

    [Fact]
    public void MigrationSeventeenIsUniqueAndTenantSequenceReachesFiftyOne()
    {
        var platformIds = Directory.GetFiles(Path.Combine(Root(), "db", "platform"), "*.sql")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name?.Length >= 3 && int.TryParse(name[..3], out _))
            .Select(name => int.Parse(name![..3])).ToArray();
        Assert.Equal(platformIds.Length, platformIds.Distinct().Count());
        Assert.Equal(21, platformIds.Max());
        Assert.Single(platformIds, id => id == 17);
        Assert.Single(platformIds, id => id == 18);
        Assert.Single(platformIds, id => id == 19);

        var tenantIds = Directory.GetFiles(Path.Combine(Root(), "db", "tenant-clinical", "migrations"), "*.sql")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name?.Length >= 4 && int.TryParse(name[..4], out _))
            .Select(name => int.Parse(name![..4])).ToArray();
        Assert.Equal(54, tenantIds.Max());
    }

    private static string Shape(string startMarker, string endMarker)
    {
        var constraintStart = Migration.IndexOf(
            "ADD CONSTRAINT CK_PlatformSecurityAuditEvent_OwnershipShape", StringComparison.Ordinal);
        var constraintEnd = Migration.IndexOf("\nGO", constraintStart, StringComparison.Ordinal);
        Assert.True(constraintStart >= 0 && constraintEnd > constraintStart);
        var constraint = Migration[constraintStart..constraintEnd];
        var start = constraint.IndexOf(startMarker, StringComparison.Ordinal);
        var end = constraint.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return constraint[start..end];
    }

    private static string Procedure() => Migration[Migration.IndexOf(
        "CREATE OR ALTER PROCEDURE dbo.PlatformSecurityAudit_RecordInvalidTenantMembership",
        StringComparison.Ordinal)..];

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static string Root() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
