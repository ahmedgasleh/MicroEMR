namespace MicroEMR.Infrastructure.Provisioning;

public sealed record TenantMigrationStatusRequest(
    Guid TenantUid,
    string TenantKey,
    string DatabaseServerKey,
    string DatabaseName,
    string SecretReference,
    string DatabaseStatus,
    string? PlatformSchemaVersion,
    DateTimeOffset? LastMigrationAt);

public sealed record AppliedTenantMigration(
    string MigrationId,
    string SchemaVersion,
    string ScriptHash,
    DateTimeOffset AppliedAt,
    string? AppliedBy);

public sealed record TenantMigrationDatabaseSnapshot(
    bool IdentityTableExists,
    bool SchemaMigrationTableExists,
    IReadOnlyCollection<Guid> TenantIdentities,
    IReadOnlyList<AppliedTenantMigration> AppliedMigrations);

public sealed record TenantMigrationHashMismatch(
    string MigrationId,
    string ExpectedHash,
    string AppliedHash);

public sealed record TenantMigrationStatusReport(
    TenantMigrationStatusRequest Tenant,
    int ManifestMigrationCount,
    bool DatabaseIdentityValid,
    bool SchemaMigrationTableExists,
    IReadOnlyList<string> MatchingMigrationIds,
    IReadOnlyList<string> MissingMigrationIds,
    IReadOnlyList<string> UnexpectedMigrationIds,
    IReadOnlyList<TenantMigrationHashMismatch> HashMismatches,
    AppliedTenantMigration? LatestAppliedMigration,
    string? CurrentSchemaVersion,
    bool IsCurrent,
    string? InspectionError)
{
    public string LastFailure =>
        string.Equals(Tenant.DatabaseStatus, "MigrationFailed", StringComparison.OrdinalIgnoreCase)
            ? "No persisted migration failure detail available."
            : "none";
}
