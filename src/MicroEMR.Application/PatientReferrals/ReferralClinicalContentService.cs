using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientAllergies.Services;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.PatientMedications.Services;
using MicroEMR.Application.PatientProblems.Services;
using MicroEMR.Application.PatientResults;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.Templates.Runtime;

namespace MicroEMR.Application.PatientReferrals;

public sealed record ReferralClinicalOption(string SelectionKind, string? CppCategoryCode, Guid? EncounterUid,
    Guid? ResultUid, string Label, DateTime? DateUtc = null, string? Provider = null, string? Status = null, Guid? FileUid = null);
public sealed record ReferralClinicalOptionsResponse(IReadOnlyList<ReferralClinicalOption> Categories,
    IReadOnlyList<ReferralClinicalOption> Encounters, IReadOnlyList<ReferralClinicalOption> Results,
    IReadOnlyList<ReferralClinicalOption>? Files = null);
public sealed record ReferralClinicalComposition(string Html, PatientReferralClinicalSelectionsResponse SelectionSet);

public interface IReferralClinicalContentService
{
    Task<ReferralClinicalOptionsResponse> GetOptionsAsync(Guid patientUid, CancellationToken token = default);
    Task<string> RenderPreviewAsync(Guid patientUid, Guid referralUid, CancellationToken token = default, string timeZoneId = "UTC");
    Task<ReferralClinicalComposition> ComposeAsync(Guid patientUid, Guid referralUid, CancellationToken token = default, string timeZoneId = "UTC");
}

public sealed class ReferralClinicalContentService(IPatientReferralRepository referrals,
    IPatientProblemService problems, IPatientAllergyService allergies, IPatientMedicationService medications,
    IPatientEncounterService encounters, IPatientResultRepository results, ICurrentUserPermissionService permissions,
    IPatientChartReadAuditService chartAudit, IStructuredReadAuditService readAudit, ITemplateInstanceRuntime runtime)
    : IReferralClinicalContentService
{
    public async Task<ReferralClinicalOptionsResponse> GetOptionsAsync(Guid patientUid, CancellationToken token = default)
    {
        var access = await Access(token);
        var correlation = Correlation();
        await chartAudit.RecordOpenedAsync(patientUid, correlation, token);
        var encounterOptions = access.Contains(PermissionKeys.EncountersView)
            ? (await encounters.GetByPatientUidAsync(patientUid, token)).Where(x => x.PatientUid == patientUid)
                .OrderByDescending(x => x.Status == "Signed").ThenByDescending(x => x.EncounterDateUtc)
                .Select(x => new ReferralClinicalOption("ENCOUNTER", null, x.EncounterUid, null,
                    x.EncounterType, x.EncounterDateUtc, x.ProviderName, x.Status)).ToArray() : [];
        var resultOptions = access.Contains(PermissionKeys.ResultsView)
            ? (await results.List(patientUid, "All", token)).Where(x => x.PatientUid == patientUid && x.LifecycleStatus == "Current")
                .OrderByDescending(x => x.ResultDate).Select(x => new ReferralClinicalOption("RESULT", null, null,
                    x.PatientResultUid, x.ResultName, x.ResultDate, null, x.ResultStatus)).ToArray() : [];
        return new([new("CPP", "PROBLEMS", null, null, "Ongoing Problems / Diagnoses"),
            new("CPP", "ALLERGIES", null, null, "Allergies"),new("CPP", "MEDICATIONS", null, null, "Medications")],
            encounterOptions, resultOptions);
    }

    public async Task<string> RenderPreviewAsync(Guid patientUid, Guid referralUid, CancellationToken token = default, string timeZoneId = "UTC")
        => (await ComposeAsync(patientUid, referralUid, token, timeZoneId)).Html;

    public async Task<ReferralClinicalComposition> ComposeAsync(Guid patientUid, Guid referralUid, CancellationToken token = default, string timeZoneId = "UTC")
    {
        var access = await Access(token);
        var selected = await referrals.GetClinicalSelectionsAsync(patientUid, referralUid, token)
            ?? throw new PatientReferralPatientNotFoundException();
        foreach (var item in selected.Selections)
        {
            var key = item.SelectionKind switch { "CPP" => PermissionKeys.PatientsView,
                "ENCOUNTER" => PermissionKeys.EncountersView, "RESULT" => PermissionKeys.ResultsView, "FILE" => PermissionKeys.DocumentsView,
                _ => throw new ReferralClinicalSelectionRuleException("Unsupported selected clinical source.") };
            if (!access.Contains(key)) throw new UnauthorizedAccessException("Selected clinical source is restricted.");
        }
        if (selected.PatientUid != patientUid || selected.ReferralUid != referralUid) throw Missing();
        if (selected.Selections.All(x => x.SelectionKind == ReferralClinicalSelectionKinds.File)) return new(string.Empty, selected);
        var correlation = Correlation();
        if (selected.Selections.Any(x => x.SelectionKind is "CPP" or "RESULT"))
            await chartAudit.RecordOpenedAsync(patientUid, correlation, token);
        var html = new StringBuilder("<section><h2>Selected Clinical Information</h2>");
        foreach (var item in selected.Selections)
        {
            switch (item.SelectionKind)
            {
                case "CPP":
                    switch (item.CppCategoryCode)
                    {
                        case "PROBLEMS":
                            Items(html,"Ongoing Problems / Diagnoses",(await problems.GetByPatientUidAsync(patientUid,"Active",token))
                                .Where(x => x.PatientUid == patientUid && x.ProblemStatus == "Active")
                                .Select(x => Join(x.ProblemName,x.ProblemDescription))); break;
                        case "ALLERGIES":
                            Items(html,"Allergies",(await allergies.GetByPatientUidAsync(patientUid,token))
                                .Where(x => x.PatientUid == patientUid && x.Status == "Active")
                                .Select(x => Join(x.AllergenName,Prefix("Reaction",x.Reaction),Prefix("Severity",x.Severity)))); break;
                        case "MEDICATIONS":
                            Items(html,"Medications",(await medications.GetByPatientUidAsync(patientUid,token))
                                .Where(x => x.PatientUid == patientUid && x.Status == "Active")
                                .Select(x => Join(x.MedicationName,x.Strength,x.DosageForm,x.Route,x.Frequency,x.Directions))); break;
                        default: throw new ReferralClinicalSelectionRuleException("Unsupported CPP category.");
                    }
                    break;
                case "ENCOUNTER":
                    var encounter = await encounters.GetByUidAsync(item.EncounterUid!.Value,token);
                    if (encounter is null || encounter.PatientUid != patientUid || encounter.EncounterUid != item.EncounterUid) throw Missing();
                    await readAudit.RecordAsync(ReadAuditActions.EncounterViewed,ReadAuditResourceTypes.Encounter,
                        encounter.EncounterUid,patientUid,correlation,token);
                    html.Append("<h3>Selected Encounter</h3>");
                    Paragraph(html,Join(Date(ClinicDate(encounter.EncounterDateUtc,timeZoneId)),encounter.EncounterType,encounter.ProviderName,encounter.Status));
                    if (encounter.TemplateDefinition is not null && !string.IsNullOrWhiteSpace(encounter.StructuredDataJson))
                    {
                        var processed = runtime.Process(encounter.TemplateDefinition,encounter.StructuredDataJson);
                        if (!processed.IsValid) throw Missing();
                        Paragraph(html,runtime.RenderSnapshot(encounter.TemplateDefinition,processed.Data!));
                    }
                    else
                    {
                        Note(html,"Subjective",encounter.SubjectiveNote); Note(html,"Objective",encounter.ObjectiveNote);
                        Note(html,"Assessment",encounter.AssessmentNote); Note(html,"Plan",encounter.PlanNote);
                        Note(html,"Notes",encounter.Notes);
                    }
                    break;
                case "RESULT":
                    var result = await results.Get(patientUid,item.ResultUid!.Value,token);
                    if (result is null || result.PatientUid != patientUid || result.PatientResultUid != item.ResultUid || result.LifecycleStatus != "Current") throw Missing();
                    html.Append("<h3>Selected Result / Report</h3>");
                    Paragraph(html,Join(Date(result.ResultDate),result.ResultName,result.ResultType,result.ResultStatus));
                    Paragraph(html,Join(result.ResultValue,result.ResultUnit,Prefix("Reference range",result.ReferenceRange),result.Abnormality));
                    Note(html,"Report summary",result.ResultSummary);
                    Note(html,"Source",Join(result.SourceType,result.SourceOrganization,result.SourceSystem));
                    break;
            }
        }
        return new(html.Append("</section>").ToString(), selected);
    }

    private async Task<IReadOnlySet<string>> Access(CancellationToken token)
    {
        var access = await permissions.GetEffectivePermissionsAsync(token);
        if (!access.Contains(PermissionKeys.PatientsView) || !access.Contains(PermissionKeys.ReferralsView)
            || !access.Contains(PermissionKeys.ReferralsManage)) throw new UnauthorizedAccessException("Draft clinical content is restricted.");
        return access;
    }
    private static ReferralClinicalSelectionRuleException Missing() => new("A selected clinical source is unavailable for this patient. Edit the Draft selections and try again.");
    private static string Correlation() => Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    private static string Date(DateTime value) => value.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
    private static DateTime ClinicDate(DateTime value,string zone)
    {
        var utc=DateTime.SpecifyKind(value,DateTimeKind.Utc);
        try {return TimeZoneInfo.ConvertTimeFromUtc(utc,TimeZoneInfo.FindSystemTimeZoneById(zone));}
        catch(TimeZoneNotFoundException) {return utc;}
        catch(InvalidTimeZoneException) {return utc;}
    }
    private static string Join(params string?[] values) => string.Join(" — ",values.Where(x => !string.IsNullOrWhiteSpace(x)));
    private static string? Prefix(string label,string? value) => string.IsNullOrWhiteSpace(value) ? null : $"{label}: {value}";
    private static void Paragraph(StringBuilder html,string? value) => html.Append("<p style=\"white-space: pre-wrap\">").Append(WebUtility.HtmlEncode(value ?? "No recorded items")).Append("</p>");
    private static void Note(StringBuilder html,string label,string? value)
    { if (!string.IsNullOrWhiteSpace(value)) { html.Append("<h4>").Append(WebUtility.HtmlEncode(label)).Append("</h4>"); Paragraph(html,value); } }
    private static void Items(StringBuilder html,string label,IEnumerable<string> items)
    {
        html.Append("<h3>").Append(WebUtility.HtmlEncode(label)).Append("</h3>");
        var rows = items.ToArray();
        if (rows.Length == 0) { Paragraph(html,"No recorded items"); return; }
        html.Append("<ul>"); foreach (var row in rows) html.Append("<li>").Append(WebUtility.HtmlEncode(row)).Append("</li>"); html.Append("</ul>");
    }
}
