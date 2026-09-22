using Microsoft.Extensions.Configuration;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class DeterministicMigrationHashingTests
{
    [Fact]
    public void CanonicalV2ProducesSameHashForLfCrlfAndCr()
    {
        const string lf = "SELECT 1;\nGO\nSELECT 2;\n";

        Assert.Equal(Canonical(lf), Canonical(lf.Replace("\n", "\r\n", StringComparison.Ordinal)));
        Assert.Equal(Canonical(lf), Canonical(lf.Replace("\n", "\r", StringComparison.Ordinal)));
    }

    [Fact]
    public void CanonicalV2IgnoresRecognizedLeadingBom()
    {
        Assert.Equal(Canonical("SELECT 1;\n"), Canonical("\uFEFFSELECT 1;\n"));
    }

    [Theory]
    [InlineData("SELECT 1;\n", "SELECT  1;\n")]
    [InlineData("SELECT 1;\n", "SELECT\t1;\n")]
    [InlineData("SELECT 1;\n", "-- comment\nSELECT 1;\n")]
    [InlineData("SELECT 1;\n", "SELECT 2;\n")]
    public void CanonicalV2KeepsNonLineEndingContentSignificant(string first, string second)
    {
        Assert.NotEqual(Canonical(first), Canonical(second));
    }

    [Fact]
    public void CanonicalV2PreservesFinalNewlineSemantics()
    {
        Assert.Equal(Canonical("SELECT 1;\n"), Canonical("SELECT 1;\r\n"));
        Assert.NotEqual(Canonical("SELECT 1;"), Canonical("SELECT 1;\n"));
    }

    [Fact]
    public async Task Historical0000LegacyHashRequiresPinnedCurrentSource()
    {
        var migration = (await Source().GetAvailableMigrationsAsync())[0];
        var legacyHash = LegacyCrlf(migration.Script);

        Assert.Equal(TenantMigrationHashMatch.ApprovedLegacyV1,
            TenantMigrationHashPolicy.Validate(migration, legacyHash));

        var changed = migration with
        {
            Script = migration.Script + "-- changed\n",
            ScriptHash = Canonical(migration.Script + "-- changed\n")
        };
        Assert.Equal(TenantMigrationHashMatch.Mismatch,
            TenantMigrationHashPolicy.Validate(changed, legacyHash));
    }

    [Theory]
    [InlineData("0014-scheduling-mark-arrived")]
    [InlineData("0015-start-encounter-status")]
    [InlineData("0016-complete-appointment-after-sign")]
    [InlineData("0017-document-template-versioning")]
    public async Task RequiredHistoricalMigrationsAcceptOnlyTheirPinnedLegacyHash(string migrationId)
    {
        var migration = (await Source().GetAvailableMigrationsAsync())
            .Single(item => item.MigrationId == migrationId);

        Assert.Equal(TenantMigrationHashMatch.ApprovedLegacyV1,
            TenantMigrationHashPolicy.Validate(migration, LegacyCrlf(migration.Script)));
    }

    [Fact]
    public async Task WrongMigrationIdCannotUseAnotherMigrationsLegacyHash()
    {
        var migrations = await Source().GetAvailableMigrationsAsync();
        var first = migrations[0];
        var second = migrations[1];

        Assert.Equal(TenantMigrationHashMatch.Mismatch,
            TenantMigrationHashPolicy.Validate(second, LegacyCrlf(first.Script)));
    }

    [Fact]
    public async Task UnregisteredLegacyHashFailsAnd0059RemainsCanonical()
    {
        var migrations = await Source().GetAvailableMigrationsAsync();
        var migration0059 = migrations.Single(item => item.MigrationId.StartsWith("0059-", StringComparison.Ordinal));

        Assert.Equal(TenantMigrationHashMatch.CanonicalV2,
            TenantMigrationHashPolicy.Validate(migration0059, migration0059.ScriptHash));
        Assert.Equal(TenantMigrationHashMatch.Mismatch,
            TenantMigrationHashPolicy.Validate(migration0059, LegacyCrlf(migration0059.Script)));
    }

    [Fact]
    public async Task CompatibleAppliedMigrationIsNotPendingAndLedgerEvidenceIsUnchanged()
    {
        var migrations = await Source().GetAvailableMigrationsAsync();
        var first = migrations[0];
        var legacyHash = LegacyCrlf(first.Script);
        var applied = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [first.MigrationId] = legacyHash
        };

        var pending = TenantDatabaseMigrationRunner.GetPendingMigrations(applied, migrations);

        Assert.DoesNotContain(pending, item => item.MigrationId == first.MigrationId);
        Assert.Equal(legacyHash, applied[first.MigrationId]);
    }

    [Fact]
    public async Task StatusIsCurrentForCanonicalAndApprovedHistoricalLegacyEvidence()
    {
        var migrations = await Source().GetAvailableMigrationsAsync();
        var canonical = Snapshot(migrations, useLegacyForFirstEighteen: false);
        var legacy = Snapshot(migrations, useLegacyForFirstEighteen: true);

        var canonicalReport = TenantMigrationStatusService.Compare(Request(), migrations, canonical);
        var legacyReport = TenantMigrationStatusService.Compare(Request(), migrations, legacy);

        Assert.True(canonicalReport.IsCurrent);
        Assert.Empty(canonicalReport.ApprovedLegacyMigrationIds);
        Assert.True(legacyReport.IsCurrent);
        Assert.Equal(18, legacyReport.ApprovedLegacyMigrationIds.Count);
        Assert.Empty(legacyReport.HashMismatches);
    }

    [Fact]
    public void PlatformFingerprintIsStableAcrossLfAndCrlfAndRejectsRealChange()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "db", "platform",
            "018_platform_entitlement_foundation.sql"));
        var lf = MigrationSourceHashing.NormalizeLineEndings(source);
        var crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);
        const string expected = "6360470B7FE4D0A1448A066F432D6D6506870BABF0989792F2512D005FA4D615";

        Assert.Equal(expected, Canonical(lf));
        Assert.Equal(expected, Canonical(crlf));
        Assert.NotEqual(expected, Canonical(lf + "-- changed\n"));
    }

    [Fact]
    public void RegistryAndCutoffAreExplicit()
    {
        Assert.Equal(18, TenantMigrationHashPolicy.RegisteredLegacyMigrationCount);
        Assert.Equal("0059", TenantMigrationHashPolicy.CanonicalV2OnlyAfterMigrationNumber);
        Assert.Contains("LegacyV1", TenantMigrationHashPolicy.GetCompatibilityReason(
            "0000-tenant-metadata"));
    }

    private static TenantMigrationDatabaseSnapshot Snapshot(
        IReadOnlyList<TenantDatabaseMigration> migrations,
        bool useLegacyForFirstEighteen) =>
        new(true, true, [TenantUid], migrations.Select((migration, index) =>
            new AppliedTenantMigration(
                migration.MigrationId,
                migration.SchemaVersion,
                useLegacyForFirstEighteen && index < 18
                    ? LegacyCrlf(migration.Script)
                    : migration.ScriptHash,
                DateTimeOffset.Parse("2026-01-01T00:00:00Z").AddMinutes(index),
                "test")).ToArray());

    private static readonly Guid TenantUid = Guid.Parse("3D9944E4-A5F3-48E8-AB7D-398B402C090B");

    private static TenantMigrationStatusRequest Request() =>
        new(TenantUid, "synthetic", "server", "database", "secret", "Active", "1.0.0", null);

    private static FileTenantDatabaseMigrationSource Source() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["TenantProvisioning:SqlAssetsPath"] = Path.Combine(AppContext.BaseDirectory, "database")
            }).Build());

    private static string LegacyCrlf(string source) =>
        MigrationSourceHashing.ComputeHash(
            MigrationSourceHashing.NormalizeLineEndings(source).Replace("\n", "\r\n", StringComparison.Ordinal),
            MigrationHashVersion.LegacyV1);

    private static string Canonical(string source) =>
        MigrationSourceHashing.ComputeCanonicalV2(source);

    private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));
}
