namespace MicroEMR.Application.Tenancy;

public interface ITenantDatabaseResolver
{
    Task<TenantDatabaseInfo?> ResolveAsync(
        Guid tenantUid,
        CancellationToken cancellationToken = default);
}
