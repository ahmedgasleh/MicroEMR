namespace MicroEMR.Application.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public TenantContext(Guid tenantUid, string tenantKey, string displayName)
    {
        if (tenantUid == Guid.Empty)
        {
            throw new ArgumentException("Tenant UID must not be empty.", nameof(tenantUid));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        TenantUid = tenantUid;
        TenantKey = tenantKey.Trim();
        DisplayName = displayName.Trim();
    }

    public Guid TenantUid { get; }
    public string TenantKey { get; }
    public string DisplayName { get; }
}
