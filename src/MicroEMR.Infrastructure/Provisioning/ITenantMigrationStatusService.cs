namespace MicroEMR.Infrastructure.Provisioning;

public interface ITenantMigrationStatusService
{
    Task<TenantMigrationStatusReport> InspectAsync(
        TenantMigrationStatusRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITenantMigrationStatusReader
{
    Task<TenantMigrationDatabaseSnapshot> ReadAsync(
        TenantMigrationStatusRequest request,
        CancellationToken cancellationToken = default);
}
