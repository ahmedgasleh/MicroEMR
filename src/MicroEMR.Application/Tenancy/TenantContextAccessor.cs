namespace MicroEMR.Application.Tenancy;

public sealed class TenantContextAccessor : ITenantContextAccessor
{
    public ITenantContext? Current { get; private set; }

    public void SetTenant(ITenantContext tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        if (Current is not null &&
            (Current.TenantUid != tenant.TenantUid ||
             !string.Equals(Current.TenantKey, tenant.TenantKey, StringComparison.Ordinal) ||
             !string.Equals(Current.DisplayName, tenant.DisplayName, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "A conflicting tenant context has already been established for the current operation.");
        }

        Current ??= tenant;
    }

    public void Clear() => Current = null;
}
