namespace MicroEMR.Infrastructure.Provisioning;

public interface ITenantDatabaseMigrationRunner
{
    Task<TenantDatabaseProvisioningResult> ProvisionAsync(
        TenantDatabaseProvisioningRequest request,
        CancellationToken cancellationToken = default);
}
