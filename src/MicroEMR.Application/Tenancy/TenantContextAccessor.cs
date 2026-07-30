namespace MicroEMR.Application.Tenancy;

public sealed class TenantContextAccessor : ITenantContextAccessor
{
    public ITenantContext? Current { get; private set; }

    public void SetTenant(ITenantContext tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        if (Current is not null && Current.TenantUid != tenant.TenantUid)
        {
            throw new InvalidOperationException(
                "A different tenant context has already been established for the current operation.");
        }

        Current ??= tenant;
    }

    public void Clear() => Current = null;
}
