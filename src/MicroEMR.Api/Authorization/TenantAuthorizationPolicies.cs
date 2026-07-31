namespace MicroEMR.Api.Authorization;

public static class TenantAuthorizationPolicies
{
    public const string ClinicAdministrator = "TenantClinicAdministrator";
    public const string SchedulingStatusManager = "TenantSchedulingStatusManager";
    public const string EncounterStarter = "TenantEncounterStarter";
}
