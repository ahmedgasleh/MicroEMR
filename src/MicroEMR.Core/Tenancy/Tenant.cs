namespace MicroEMR.Core.Tenancy;

public sealed class Tenant
{
    public Tenant(
        Guid tenantUid,
        string tenantKey,
        string displayName,
        TenantStatus status,
        string defaultTimeZoneId,
        DateTimeOffset createdAt,
        DateTimeOffset? activatedAt = null,
        DateTimeOffset? suspendedAt = null)
    {
        if (tenantUid == Guid.Empty)
        {
            throw new ArgumentException("Tenant UID must not be empty.", nameof(tenantUid));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultTimeZoneId);

        TenantUid = tenantUid;
        TenantKey = tenantKey.Trim();
        DisplayName = displayName.Trim();
        Status = status;
        DefaultTimeZoneId = defaultTimeZoneId.Trim();
        CreatedAt = createdAt;
        ActivatedAt = activatedAt;
        SuspendedAt = suspendedAt;
    }

    public Guid TenantUid { get; }

    public string TenantKey { get; }

    public string DisplayName { get; }

    public TenantStatus Status { get; }

    public string DefaultTimeZoneId { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? ActivatedAt { get; }

    public DateTimeOffset? SuspendedAt { get; }
}
