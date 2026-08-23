using MicroEMR.Application.Tenancy;

namespace MicroEMR.Api;

public sealed class DeferredTenantContext(ITenantContextAccessor accessor) : ITenantContext
{
    public Guid TenantUid => Current.TenantUid;
    public string TenantKey => Current.TenantKey;
    public string DisplayName => Current.DisplayName;

    private ITenantContext Current => accessor.Current
        ?? throw new InvalidOperationException(
            "Tenant context has not been established for the current operation.");
}
