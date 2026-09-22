namespace MicroEMR.Infrastructure.Provisioning;

public sealed class TenantMigrationStatusService(
    ITenantDatabaseMigrationSource migrationSource,
    ITenantMigrationStatusReader reader) : ITenantMigrationStatusService
{
    public async Task<TenantMigrationStatusReport> InspectAsync(
        TenantMigrationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var manifest = await migrationSource.GetAvailableMigrationsAsync(cancellationToken);
        try
        {
            var snapshot = await reader.ReadAsync(request, cancellationToken);
            return Compare(request, manifest, snapshot);
        }
        catch (Exception exception)
        {
            return new(request, manifest.Count, false, false, [], [], [], [], null,
                request.PlatformSchemaVersion, false, exception.Message);
        }
    }

    public static TenantMigrationStatusReport Compare(
        TenantMigrationStatusRequest request,
        IReadOnlyCollection<TenantDatabaseMigration> manifest,
        TenantMigrationDatabaseSnapshot snapshot)
    {
        var identityValid = snapshot.IdentityTableExists &&
            snapshot.TenantIdentities.Count == 1 &&
            snapshot.TenantIdentities.Single() == request.TenantUid;
        if (!identityValid)
            return new(request, manifest.Count, false, snapshot.SchemaMigrationTableExists, [], [], [], [],
                null, request.PlatformSchemaVersion, false,
                snapshot.IdentityTableExists
                    ? "Database identity does not match the requested tenant."
                    : "dbo.TenantDatabaseIdentity does not exist.");

        if (!snapshot.SchemaMigrationTableExists)
            return new(request, manifest.Count, true, false, [], manifest.Select(x => x.MigrationId).ToArray(),
                [], [], null, request.PlatformSchemaVersion, false,
                "dbo.SchemaMigration does not exist.");

        var expected = manifest.ToDictionary(x => x.MigrationId, StringComparer.Ordinal);
        var applied = snapshot.AppliedMigrations.ToDictionary(x => x.MigrationId, StringComparer.Ordinal);
        var missing = expected.Keys.Except(applied.Keys, StringComparer.Ordinal).Order().ToArray();
        var unexpected = applied.Keys.Except(expected.Keys, StringComparer.Ordinal).Order().ToArray();
        var comparisons = expected.Keys.Intersect(applied.Keys, StringComparer.Ordinal)
            .ToDictionary(
                id => id,
                id => TenantMigrationHashPolicy.Validate(expected[id], applied[id].ScriptHash),
                StringComparer.Ordinal);
        var mismatches = comparisons
            .Where(item => item.Value == TenantMigrationHashMatch.Mismatch)
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new TenantMigrationHashMismatch(
                item.Key, expected[item.Key].ScriptHash, applied[item.Key].ScriptHash))
            .ToArray();
        var matching = comparisons
            .Where(item => item.Value != TenantMigrationHashMatch.Mismatch)
            .Select(item => item.Key)
            .Order().ToArray();
        var approvedLegacy = comparisons
            .Where(item => item.Value == TenantMigrationHashMatch.ApprovedLegacyV1)
            .Select(item => item.Key)
            .Order().ToArray();
        var latest = snapshot.AppliedMigrations.OrderByDescending(x => x.AppliedAt).FirstOrDefault();
        var failed = string.Equals(request.DatabaseStatus, "MigrationFailed", StringComparison.OrdinalIgnoreCase);
        var current = missing.Length == 0 && unexpected.Length == 0 && mismatches.Length == 0 && !failed;
        return new(request, manifest.Count, true, true, matching, missing, unexpected, mismatches, latest,
            latest?.SchemaVersion ?? request.PlatformSchemaVersion, current, null)
        {
            ApprovedLegacyMigrationIds = approvedLegacy
        };
    }
}
