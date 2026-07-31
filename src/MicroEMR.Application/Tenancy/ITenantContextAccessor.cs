namespace MicroEMR.Application.Tenancy;

public interface ITenantContextAccessor
{
    ITenantContext? Current { get; }

    void SetTenant(ITenantContext tenant);

    void Clear();
}
