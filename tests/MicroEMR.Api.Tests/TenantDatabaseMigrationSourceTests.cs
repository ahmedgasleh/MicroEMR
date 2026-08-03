using Microsoft.Extensions.Configuration;
using MicroEMR.Infrastructure.Provisioning;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TenantDatabaseMigrationSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "MicroEMR-MigrationTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MigrationsAreOrderedAndHashIsStable()
    {
        Write("one.sql", "SELECT 1;");
        Write("two.sql", "SELECT 2;");
        Manifest("""
            [
              { "migrationId":"0002", "schemaVersion":"1.0.0", "script":"two.sql" },
              { "migrationId":"0001", "schemaVersion":"1.0.0", "script":"one.sql" }
            ]
            """);

        var first = await Source().GetAvailableMigrationsAsync();
        var second = await Source().GetAvailableMigrationsAsync();

        Assert.Equal(["0001", "0002"], first.Select(item => item.MigrationId));
        Assert.Equal(first[0].ScriptHash, second[0].ScriptHash);
        Assert.Equal(64, first[0].ScriptHash.Length);
    }

    [Fact]
    public async Task DuplicateIdEmptyAndMissingScriptsAreRejected()
    {
        Write("empty.sql", " ");
        Manifest("""
            [
              { "migrationId":"0001", "schemaVersion":"1", "script":"empty.sql" },
              { "migrationId":"0001", "schemaVersion":"1", "script":"missing.sql" }
            ]
            """);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Source().GetAvailableMigrationsAsync());

        Manifest("[{\"migrationId\":\"0001\",\"schemaVersion\":\"1\",\"script\":\"empty.sql\"}]");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Source().GetAvailableMigrationsAsync());

        Manifest("[{\"migrationId\":\"0001\",\"schemaVersion\":\"1\",\"script\":\"missing.sql\"}]");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Source().GetAvailableMigrationsAsync());
    }

    [Fact]
    public async Task CanonicalManifestLoadsAndEveryScriptHasValidBatches()
    {
        var repositoryDb = Path.Combine(AppContext.BaseDirectory, "database");
        var source = new FileTenantDatabaseMigrationSource(
            new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["TenantProvisioning:SqlAssetsPath"] = repositoryDb
                }).Build());

        var migrations = await source.GetAvailableMigrationsAsync();

        Assert.Equal(26, migrations.Count);
        Assert.All(migrations, migration =>
            Assert.NotEmpty(SqlBatchParser.Parse(migration.Script)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private FileTenantDatabaseMigrationSource Source() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["TenantProvisioning:SqlAssetsPath"] = _root
            }).Build());

    private void Manifest(string json) =>
        Write(Path.Combine("tenant-clinical", "manifest.json"), json);

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
