using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Tenancy;

namespace MicroEMR.Application.ClinicConfiguration;

public interface IClinicConfigurationService
{
    Task<ClinicConfigurationResponse> GetAsync(CancellationToken cancellationToken = default);
    Task<ClinicConfigurationResponse> SaveAsync(SaveClinicConfigurationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ClinicConfigurationService(
    IClinicProfileRepository repository,
    ITenantContext tenantContext,
    ITenantCatalog tenantCatalog,
    IAuthenticatedClinicalUserAccessor actorAccessor) : IClinicConfigurationService
{
    public async Task<ClinicConfigurationResponse> GetAsync(CancellationToken cancellationToken = default) =>
        Map(await repository.GetAsync(cancellationToken), await GetTenantAsync(cancellationToken));

    public async Task<ClinicConfigurationResponse> SaveAsync(
        SaveClinicConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actorUserId = await actorAccessor.GetRequiredUserIdAsync(cancellationToken);
        var profile = await repository.SaveAsync(request, actorUserId, cancellationToken);
        return Map(profile, await GetTenantAsync(cancellationToken));
    }

    private async Task<MicroEMR.Core.Tenancy.Tenant> GetTenantAsync(CancellationToken cancellationToken) =>
        await tenantCatalog.GetByUidAsync(tenantContext.TenantUid, cancellationToken)
        ?? throw new InvalidOperationException("The active tenant no longer exists in the platform catalog.");

    private static ClinicConfigurationResponse Map(ClinicProfileData? profile, MicroEMR.Core.Tenancy.Tenant tenant) =>
        new(tenant.DisplayName, tenant.DefaultTimeZoneId, profile?.LegalName, profile?.Phone, profile?.Fax,
            profile?.Email, profile?.AddressLine1, profile?.AddressLine2, profile?.City, profile?.ProvinceState,
            profile?.PostalCode, profile?.Country, profile?.DefaultAppointmentDurationMinutes,
            profile?.UpdatedAtUtc, profile?.UpdatedBy, profile?.RowVersion);
}
