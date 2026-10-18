using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Application.Patients.Repositories;
using MicroEMR.Application.Providers;

namespace MicroEMR.Application.PatientCpp;

public sealed class CppPrintService(IPatientCppService cpp, IPatientRepository patients,
    IProviderAdministrationRepository providers, IClinicConfigurationService clinic,
    IPatientClinicalHistoryService history, ICurrentUserPermissionService permissions,
    IAuthenticatedClinicalUserAccessor actor, IClinicalPrintLayoutRenderer layout,
    IPdfRenderer pdf, IPdfPageNumberer numbers, TimeProvider? clock = null) : ICppPrintService
{
    public async Task<CppPrintOptions> GetOptionsAsync(CancellationToken token = default)
    {
        await RequireAccess(token);
        return new(CppDisplayCatalog.Categories, (await providers.ListAsync("Active", token)).Where(x => x.IsActive)
            .Select(x => new CppPrintClinician(x.ProviderUid, x.DisplayName, x.ProviderType)).ToArray());
    }

    public async Task<byte[]?> PrintAsync(Guid patientUid, CppPrintRequest request, string correlation, CancellationToken token = default)
    {
        CppPrintSelection.Validate(request.Categories);
        MedicationPrintScopes.Validate(request.MedicationScope);
        if (patientUid == Guid.Empty || request.ClinicianUid == Guid.Empty) throw new ArgumentException("Patient and clinician are required.");
        await RequireAccess(token);
        var clinician = await providers.GetAsync(request.ClinicianUid, token);
        if (clinician is null || !clinician.IsActive || clinician.ProviderUid != request.ClinicianUid)
            throw new ArgumentException("Select an available clinician for the letterhead.");
        var data = await cpp.GetForPrintAsync(patientUid, request.Categories, correlation, token, request.MedicationScope);
        if (data is null) return null;
        if (data.PatientUid != patientUid) throw new UnauthorizedAccessException("Patient context is unavailable.");
        var patient = await patients.GetByUidAsync(patientUid, token);
        if (patient is null) return null;
        if (patient.PatientUid != patientUid) throw new UnauthorizedAccessException("Patient context is unavailable.");
        var configuration = await clinic.GetAsync(token);
        var name = string.IsNullOrWhiteSpace(configuration.LegalName) ? configuration.ClinicName : configuration.LegalName;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(clinician.DisplayName))
            throw new CppPrintUnavailableException("Configure the clinic and clinician letterhead before printing CPP.");
        var body = new StringBuilder("<style>.cpp-print-category h2{break-after:avoid}.cpp-print-item{white-space:pre-wrap;overflow-wrap:anywhere;break-inside:auto;margin-bottom:12pt}.cpp-print-item dl{display:grid;grid-template-columns:42mm minmax(0,1fr);column-gap:3mm;row-gap:2pt;align-items:start;margin:0}.cpp-print-item dt{font-weight:bold;white-space:normal}.cpp-print-item dd{margin:0;min-width:0;white-space:pre-wrap}</style>");
        foreach (var category in CppDisplayCatalog.Categories.Where(x => request.Categories.Contains(x.Key)))
        {
            body.Append($"<section class=\"cpp-print-category\"><h2>{E(category.Key == "Medications" ? $"Medications - {MedicationPrintScopes.Label(request.MedicationScope)}" : category.Label)}</h2>");
            switch (category.Key)
            {
                case "RiskFactors": Append(body,data.RiskFactors); break;
                case "FamilyHistory": Append(body,data.FamilyHistory); break;
                case "Problems": Append(body,data.ProblemsForPrint ?? throw new CppPrintUnavailableException("Problem details are unavailable. Reload before printing.")); break;
                case "Allergies": Append(body,data.AllergiesForPrint ?? throw new CppPrintUnavailableException("Allergy details are unavailable. Reload before printing.")); break;
                case "Medications":
                    if (data.Medications.State == PatientCppSectionStates.NotDocumented)
                        body.Append($"<p>No {E(MedicationPrintScopes.Label(request.MedicationScope).ToLowerInvariant())} medications recorded.</p>");
                    else Append(body,data.Medications);
                    break;
                case "Prescriptions": Append(body,data.Prescriptions); break;
                case "Immunizations":
                    var immunizationDetails=data.ImmunizationsForPrint ?? throw new CppPrintUnavailableException("Immunization details are unavailable. Reload before printing.");
                    if(immunizationDetails.State is PatientCppSectionStates.Unavailable or PatientCppSectionStates.NotAuthorized)
                        throw new CppPrintUnavailableException("Immunization details are unavailable.");
                    foreach(var outcome in new[]{"Administered","Refused"})
                    {
                        body.Append(outcome=="Refused" ? "<h3>Refused immunizations</h3>" : "<h3>Administered immunizations</h3>");
                        var events=immunizationDetails.Items.Where(x=>x.Outcome==outcome).ToArray();
                        Append(body,PatientCppSection<PatientCppImmunizationPrint>.From(events,events.Length));
                    }
                    break;
                case "Results": Append(body,data.Results); break;
                case "Vitals": Append(body,data.Vitals); break;
                case "Encounters": Append(body,data.Encounters); break;
                case "Referrals": Append(body,data.Referrals); break;
                case "Alerts": Append(body,data.Alerts); break;
                case "Documents": Append(body,data.Documents); break;
                case "History":
                    var rows = await history.ListAsync(patientUid,"Active",token);
                    if (rows.Any(x => x.PatientUid != patientUid)) throw new UnauthorizedAccessException("History ownership is invalid.");
                    Append(body,PatientCppSection<PrintHistory>.From(rows.Where(x => x.Status == "Active")
                        .OrderByDescending(x => x.RelevantDate).ThenBy(x => x.HistoryUid)
                        .Select(x => new PrintHistory(x.HistoryType,x.Description,x.RelevantDate,x.ClinicalFields?.OnsetDate?.Display,x.ClinicalFields?.ResolutionDate?.Display,x.ClinicalFields?.ProcedureDate?.Display,x.ClinicalFields?.LifeStage,x.ClinicalFields?.ClinicalStatus,x.ClinicalFields?.Notes)).ToArray(),rows.Count));
                    break;
            }
            body.Append("</section>");
        }
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var context = new ClinicalPrintContext(
            new(name!,configuration.AddressLine1,configuration.AddressLine2,configuration.City,configuration.ProvinceState,
                configuration.PostalCode,configuration.Phone,configuration.Fax,configuration.Email),
            new(patient.FullName,patient.DateOfBirth,Recorded(patient.HealthCardNumber),string.IsNullOrWhiteSpace(patient.HealthCardNumber)?null:patient.HealthCardVersion,null)
            {
                DateOfBirthDisplay = string.Empty,
                Address = Recorded(string.Join(", ",new[] {patient.AddressLine1,patient.AddressLine2,patient.City,patient.Province,patient.PostalCode}.Where(x => !string.IsNullOrWhiteSpace(x)))),
                Phone = Recorded(patient.PhoneNumber)
            },
            new("CPP","Cumulative Patient Profile","Selected categories",now,clinician.DisplayName),
            new("Clinician",string.Join(" / ",new[] {clinician.DisplayName,clinician.ProviderType,clinician.Specialty}.Where(x => !string.IsNullOrWhiteSpace(x))),null,null,null),
            configuration.TimeZoneId);
        var rendered = await pdf.RenderAsync(layout.Render(context,body.ToString()),token);
        return await numbers.NumberAsync(rendered,token);
    }

    private async Task RequireAccess(CancellationToken token)
    {
        if (!(await permissions.GetEffectivePermissionsAsync(token)).Contains(PermissionKeys.PatientsView))
            throw new UnauthorizedAccessException("CPP printing is restricted.");
        await actor.GetRequiredUserIdAsync(token);
    }

    private static void Append<T>(StringBuilder body, PatientCppSection<T> section)
    {
        if (section.State is PatientCppSectionStates.Unavailable or PatientCppSectionStates.NotAuthorized)
            throw new CppPrintUnavailableException("A selected CPP category is unavailable. Retry or remove that category before printing.");
        if (section.State == PatientCppSectionStates.ExplicitlyNone) { body.Append("<p>No Known Allergies</p>"); return; }
        if (section.Items.Count == 0) { body.Append("<p>No eligible records documented.</p>"); return; }
        foreach (var item in section.Items)
        {
            body.Append("<div class=\"cpp-print-item\"><dl>");
            // Print every clinical field in the established CPP projection. Internal UIDs are not output.
            foreach (var property in typeof(T).GetProperties().Where(x => !x.Name.EndsWith("Uid",StringComparison.Ordinal)))
            {
                if (item is PrintHistory history &&
                    (history.HistoryType == "Medical" && property.Name == "ProcedureDate" ||
                     history.HistoryType == "Surgical" && property.Name is "OnsetDate" or "ClinicalResolutionDate" or "LifeStage" or "ClinicalStatus")) continue;
                if(item is PatientCppImmunizationPrint immunization &&
                    (immunization.Outcome=="Refused" && property.Name is "AdministrationDate" or "DIN" or "Manufacturer" or "DoseNumber" or "Route" or "Site" or "LotNumber" or "SourceType" or "AdministeredByName" ||
                     immunization.Outcome=="Administered" && property.Name=="RefusalDate")) continue;
                var value = property.GetValue(item);
                var display = value switch
                {
                    DateOnly date => date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
                    DateTime date => date.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture),
                    IFormattable number => number.ToString(null,CultureInfo.InvariantCulture),
                    _ => value?.ToString()
                };
                body.Append($"<dt>{E(Regex.Replace(property.Name,"([a-z])([A-Z])","$1 $2"))}</dt><dd>{E(Recorded(display))}</dd>");
            }
            body.Append("</dl></div>");
        }
    }
    private static string Recorded(string? value) => string.IsNullOrWhiteSpace(value) ? "Not recorded" : value;
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    private sealed record PrintHistory(string HistoryType,string Description,DateOnly? RelevantDate,string? OnsetDate,string? ClinicalResolutionDate,string? ProcedureDate,string? LifeStage,string? ClinicalStatus,string? Notes);
}
