namespace MicroEMR.Web.Authorization;

public static class ClinicConfigurationAuthorization
{
    public const string Policy = "TenantClinicAdministrator";
    public const string Role = MicroEMR.Application.PlatformAdministration.TenantRoleCatalog.ClinicAdministrator;
}
