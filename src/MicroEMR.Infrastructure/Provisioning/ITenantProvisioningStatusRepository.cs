namespace MicroEMR.Infrastructure.Provisioning;

public interface ITenantProvisioningStatusRepository
{
    Task MarkStartedAsync(Guid tenantUid, CancellationToken cancellationToken = default);
    Task MarkCompletedAsync(Guid tenantUid, string schemaVersion, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(Guid tenantUid, CancellationToken cancellationToken = default);
}
