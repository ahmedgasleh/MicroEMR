using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace MicroEMR.Infrastructure.Provisioning;

public sealed class FileTenantDatabaseMigrationSource
    : ITenantDatabaseMigrationSource
{
    private readonly string _assetsRoot;

    public FileTenantDatabaseMigrationSource(IConfiguration configuration)
    {
        _assetsRoot = Path.GetFullPath(
            configuration["TenantProvisioning:SqlAssetsPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "database"));
    }

    public async Task<IReadOnlyList<TenantDatabaseMigration>>
        GetAvailableMigrationsAsync(CancellationToken cancellationToken = default)
    {
        var manifestPath = Path.Combine(
            _assetsRoot,
            "tenant-clinical",
            "manifest.json");
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException("The tenant migration manifest was not found.");

        await using var stream = File.OpenRead(manifestPath);
        var entries = await JsonSerializer.DeserializeAsync<List<ManifestEntry>>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken) ?? [];

        var duplicate = entries
            .GroupBy(entry => entry.MigrationId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException(
                $"Duplicate tenant migration ID '{duplicate.Key}' was found.");

        var migrations = new List<TenantDatabaseMigration>(entries.Count);
        foreach (var entry in entries.OrderBy(entry => entry.MigrationId, StringComparer.Ordinal))
        {
            ValidateEntry(entry);
            var scriptPath = ResolveControlledPath(entry.Script);
            if (!File.Exists(scriptPath))
                throw new InvalidOperationException(
                    $"Tenant migration script '{entry.Script}' was not found.");

            var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
            if (string.IsNullOrWhiteSpace(script))
                throw new InvalidOperationException(
                    $"Tenant migration script '{entry.Script}' is empty.");

            var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(script)));
            migrations.Add(new TenantDatabaseMigration(
                entry.MigrationId,
                entry.SchemaVersion,
                entry.Script,
                hash,
                script));
        }

        return migrations;
    }

    private string ResolveControlledPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_assetsRoot, relativePath));
        var prefix = _assetsRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _assetsRoot
            : _assetsRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Tenant migration paths must remain inside the controlled SQL assets directory.");
        return fullPath;
    }

    private static void ValidateEntry(ManifestEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.MigrationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.SchemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Script);
    }

    private sealed record ManifestEntry(
        string MigrationId,
        string SchemaVersion,
        string Script);
}
