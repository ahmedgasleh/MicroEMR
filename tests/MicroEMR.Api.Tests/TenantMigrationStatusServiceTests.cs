using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TenantMigrationStatusServiceTests
{
    [Fact]
    public void Fully_current_tenant_is_current() => Assert.True(Compare(Applied("0001"), Applied("0002")).IsCurrent);

    [Fact]
    public void One_missing_migration_is_reported() =>
        Assert.Equal(["0002"], Compare(Applied("0001")).MissingMigrationIds);

    [Fact]
    public void Multiple_missing_migrations_are_reported() =>
        Assert.Equal(["0001", "0002"], Compare().MissingMigrationIds);

    [Fact]
    public void Unknown_applied_migration_is_reported() =>
        Assert.Equal(["9999"], Compare(Applied("0001"), Applied("0002"), Applied("9999")).UnexpectedMigrationIds);

    [Fact]
    public void Hash_mismatch_is_reported()
    {
        var report = Compare(Applied("0001", "wrong"), Applied("0002"));
        Assert.Equal("0001", Assert.Single(report.HashMismatches).MigrationId);
        Assert.False(report.IsCurrent);
    }

    [Fact]
    public void Missing_schema_migration_table_is_initialization_problem()
    {
        var snapshot = new TenantMigrationDatabaseSnapshot(true, false, [TenantUid], []);
        var report = TenantMigrationStatusService.Compare(Request(), Manifest(), snapshot);
        Assert.Contains("does not exist", report.InspectionError);
        Assert.Equal(["0001", "0002"], report.MissingMigrationIds);
    }

    [Fact]
    public void Migration_failed_platform_state_is_not_current()
    {
        var report = Compare([Applied("0001"), Applied("0002")], "MigrationFailed");
        Assert.False(report.IsCurrent);
        Assert.Equal("No persisted migration failure detail available.", report.LastFailure);
    }

    [Fact]
    public void Non_failed_platform_state_reports_no_failure() =>
        Assert.Equal("none", Compare(Applied("0001"), Applied("0002")).LastFailure);

    [Fact]
    public void All_aggregation_fails_when_any_tenant_has_drift()
    {
        var reports = new[] { Compare(Applied("0001"), Applied("0002")), Compare(Applied("0001")) };
        Assert.False(reports.All(x => x.IsCurrent));
    }

    [Fact]
    public void Status_service_has_no_migration_runner_dependency()
    {
        var dependencies = typeof(TenantMigrationStatusService).GetConstructors().Single()
            .GetParameters().Select(x => x.ParameterType).ToArray();
        Assert.DoesNotContain(typeof(ITenantDatabaseMigrationRunner), dependencies);
    }

    [Fact]
    public void Invalid_database_identity_stops_metadata_comparison()
    {
        var snapshot = new TenantMigrationDatabaseSnapshot(true, true, [Guid.NewGuid()], [Applied("0001")]);
        var report = TenantMigrationStatusService.Compare(Request(), Manifest(), snapshot);
        Assert.False(report.DatabaseIdentityValid);
        Assert.Empty(report.MatchingMigrationIds);
    }

    private static readonly Guid TenantUid = Guid.Parse("a99c3c35-8195-41fa-939c-652256156e6c");

    private static TenantMigrationStatusReport Compare(params AppliedTenantMigration[] applied) =>
        Compare(applied, "Active");

    private static TenantMigrationStatusReport Compare(
        IReadOnlyList<AppliedTenantMigration> applied,
        string databaseStatus)
    {
        var snapshot = new TenantMigrationDatabaseSnapshot(true, true, [TenantUid], applied);
        return TenantMigrationStatusService.Compare(Request(databaseStatus), Manifest(), snapshot);
    }

    private static TenantMigrationStatusRequest Request(string databaseStatus = "Active") =>
        new(TenantUid, "test", "server", "database", "secret", databaseStatus, null, null);

    private static IReadOnlyList<TenantDatabaseMigration> Manifest() =>
    [
        new("0001", "1.0.0", "one.sql", "hash-0001", "SELECT 1;"),
        new("0002", "1.0.0", "two.sql", "hash-0002", "SELECT 2;")
    ];

    private static AppliedTenantMigration Applied(string id, string? hash = null) =>
        new(id, "1.0.0", hash ?? $"hash-{id}", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), "test");
}
