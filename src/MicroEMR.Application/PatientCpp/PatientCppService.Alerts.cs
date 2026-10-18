namespace MicroEMR.Application.PatientCpp;

public sealed partial class PatientCppService
{
    private async Task<PatientCppSection<PatientCppChartAlert>> LoadAlerts(Guid patientUid, string trace, CancellationToken token, bool full = false) =>
        await Load("Alerts", trace, async () =>
        {
            if (chartAlerts is null) throw new InvalidOperationException("Chart Alerts source is unavailable.");
            var rows = await chartAlerts.ListAsync(patientUid, "All", token);
            EnsureOwned(rows, patientUid);
            var active = rows.Where(x => x.AlertStatus == "Active")
                .OrderBy(x => x.AlertPriority switch { "Critical" => 0, "High" => 1, "Normal" => 2, _ => 3 })
                .ThenByDescending(x => x.CreatedAt).ThenBy(x => x.PatientChartAlertUid).ToArray();
            return PatientCppSection<PatientCppChartAlert>.From(active.Take(full ? int.MaxValue : ClinicalLimit)
                .Select(x => new PatientCppChartAlert(x.AlertTitle, x.AlertMessage, x.AlertType, x.AlertPriority, x.AlertStatus)).ToArray(), active.Length);
        });
}
