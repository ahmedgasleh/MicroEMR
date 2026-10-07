using System.Globalization;
using System.Net;
using System.Text;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.Patients.Services;
using MicroEMR.Application.ReadAudit;

namespace MicroEMR.Application.PatientImmunizations;

public interface IPatientImmunizationSummaryService
{
    Task<byte[]?> GenerateAsync(Guid patientUid, string requestCorrelationId, CancellationToken token = default);
}

public sealed class PatientImmunizationSummaryService(
    IPatientService patients,
    IPatientImmunizationService immunizations,
    IClinicConfigurationService clinics,
    IClinicalPrintLayoutRenderer layout,
    IPdfRenderer pdf,
    IPatientChartReadAuditService readAudit,
    TimeProvider clock) : IPatientImmunizationSummaryService
{
    public async Task<byte[]?> GenerateAsync(Guid patientUid, string requestCorrelationId, CancellationToken token = default)
    {
        if (patientUid == Guid.Empty) return null;
        var patient = await patients.GetByUidAsync(patientUid, token);
        if (patient is null) return null;
        if (patient.PatientUid != patientUid)
            throw new InvalidOperationException("The summary patient does not match the requested patient.");

        // Reuse the governed chart-read audit before reading/disclosing clinical history.
        await readAudit.RecordOpenedAsync(patientUid, requestCorrelationId, token);
        var history = await immunizations.ListAsync(patientUid, "All", token);
        if (history.Any(item => item.PatientUid != patientUid))
            throw new InvalidOperationException("The immunization history does not belong to the requested patient.");
        var clinic = await clinics.GetAsync(token);
        var hasHealthCard = !string.IsNullOrWhiteSpace(patient.HealthCardNumber);
        var context = new ClinicalPrintContext(
            new(string.IsNullOrWhiteSpace(clinic.LegalName) ? clinic.ClinicName : clinic.LegalName,
                clinic.AddressLine1, clinic.AddressLine2, clinic.City, clinic.ProvinceState,
                clinic.PostalCode, clinic.Phone, clinic.Fax, clinic.Email),
            new(Value(patient.FullName), patient.DateOfBirth, Value(patient.HealthCardNumber),
                hasHealthCard ? patient.HealthCardVersion : null, patient.ChartNumber)
            {
                DateOfBirthDisplay = patient.DateOfBirth == default ? "Not recorded" : null
            },
            new("Summary", "Immunization Summary", "Immunization History", clock.GetUtcNow().UtcDateTime, null),
            new(null, null, null, null, null), clinic.TimeZoneId);

        var body = new StringBuilder("""
            <style>
            .immunization-summary { table-layout: fixed; }
            .immunization-summary th, .immunization-summary td { overflow-wrap: anywhere; vertical-align: top; }
            .immunization-summary thead { display: table-header-group; }
            </style><h1>Immunization Summary</h1><h2>Immunization History</h2>
            """);
        if (history.Count == 0)
            body.Append("<p>No immunizations recorded.</p>");
        else
        {
            body.Append("<p>Complete recorded history. Entries marked Entered in error are retained for history and are not valid immunization administrations.</p>")
                .Append("<table class=\"immunization-summary\"><thead><tr><th>Immunization</th><th>Date</th><th>Administering Clinician</th><th>Status</th></tr></thead><tbody>");
            foreach (var item in history.OrderByDescending(item => item.AdministrationDate)
                         .ThenByDescending(item => item.CreatedAtUtc).ThenBy(item => item.ImmunizationUid))
            {
                body.Append("<tr><td>").Append(E(Value(item.VaccineName)))
                    .Append("</td><td>").Append(item.AdministrationDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture))
                    .Append("</td><td>").Append(E(Value(item.AdministeredByName)))
                    .Append("</td><td>").Append(E(item.Status == "EnteredInError" ? "Entered in error" : item.Status))
                    .Append("</td></tr>");
            }
            body.Append("</tbody></table>");
        }

        var bytes = await pdf.RenderAsync(layout.Render(context, body.ToString()), token);
        if (bytes.Length == 0) throw new PdfRenderingException("The immunization summary could not be rendered.");
        return bytes;
    }

    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "Not recorded" : value;
    private static string E(string value) => WebUtility.HtmlEncode(value);
}
