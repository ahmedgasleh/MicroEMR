namespace MicroEMR.Application.Tenancy;

public interface ITenantContext
{
    Guid TenantUid { get; }

    string TenantKey { get; }

    string DisplayName { get; }
}
