using MicroEMR.Core.Tenancy;

namespace MicroEMR.Application.Tenancy;

public interface ITenantCatalog
{
    Task<Tenant?> GetByUidAsync(
        Guid tenantUid,
        CancellationToken cancellationToken = default);

    Task<Tenant?> GetByKeyAsync(
        string tenantKey,
        CancellationToken cancellationToken = default);
}
