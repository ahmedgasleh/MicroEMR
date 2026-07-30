namespace MicroEMR.Infrastructure.Provisioning;

public sealed record TenantDatabaseMigration(
    string MigrationId,
    string SchemaVersion,
    string ScriptPath,
    string ScriptHash,
    string Script);

public sealed record TenantDatabaseProvisioningRequest(
    Guid TenantUid,
    string TenantKey,
    string DatabaseServerKey,
    string DatabaseName,
    string SecretReference);

public enum TenantDatabaseProvisioningStatus
{
    AlreadyCurrent,
    Provisioned,
    Migrated,
    Failed
}

public sealed record TenantDatabaseProvisioningResult(
    TenantDatabaseProvisioningStatus Status,
    string CurrentSchemaVersion,
    IReadOnlyCollection<string> AppliedMigrations);
