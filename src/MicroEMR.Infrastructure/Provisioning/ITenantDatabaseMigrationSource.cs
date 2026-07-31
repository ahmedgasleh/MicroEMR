namespace MicroEMR.Infrastructure.Provisioning;

public interface ITenantDatabaseMigrationSource
{
    Task<IReadOnlyList<TenantDatabaseMigration>> GetAvailableMigrationsAsync(
        CancellationToken cancellationToken = default);
}
