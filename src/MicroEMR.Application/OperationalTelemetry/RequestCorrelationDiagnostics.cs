namespace MicroEMR.Application.OperationalTelemetry;

public static class RequestCorrelationDiagnostics
{
    public const string EnabledKey = "Diagnostics:RequestCorrelation:Enabled";

    public static bool ShouldCapture(bool isDevelopment, bool enabled, string service, string method,
        string routeTemplate, string? controller = null, string? action = null)
    {
        if (!isDevelopment || !enabled || method != "GET") return false;

        return service switch
        {
            "MicroEMR.Api" => routeTemplate is
                "api/patients/{patientUid:guid}/cpp" or
                "api/patients/{patientUid:guid}/allergies/snapshot" or
                "api/patients/{patientUid:guid}/prescriptions" or
                "api/patients/{patientUid:guid}/chart-alerts",
            "MicroEMR.Web" => (controller, action) is
                ("Patients", "Details") or ("PatientChartAlerts", "List"),
            _ => false
        };
    }
}
