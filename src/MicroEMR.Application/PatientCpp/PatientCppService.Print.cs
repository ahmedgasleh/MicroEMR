using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Application.PatientCpp;

public sealed partial class PatientCppService
{
    public async Task<PatientCppSummaryResponse?> GetForPrintAsync(Guid patientUid, IReadOnlyList<string> categories,
        string correlation, CancellationToken token = default, string medicationScope = "Ongoing")
    {
        CppPrintSelection.Validate(categories);
        MedicationPrintScopes.Validate(medicationScope);
        if (patientUid == Guid.Empty) throw new ArgumentException("A patient is required.");
        var access = await permissions.GetEffectivePermissionsAsync(token);
        if (!access.Contains(PermissionKeys.PatientsView)) throw new UnauthorizedAccessException("CPP printing is restricted.");
        foreach (var category in categories)
        {
            var permission = category switch { "Results" => PermissionKeys.ResultsView, "Encounters" => PermissionKeys.EncountersView,
                "Referrals" => PermissionKeys.ReferralsView, "Documents" => PermissionKeys.DocumentsView, _ => PermissionKeys.PatientsView };
            if (!access.Contains(permission)) throw new UnauthorizedAccessException("A selected CPP category is restricted. Remove it before printing.");
        }
        var patient = await patients.GetByUidAsync(patientUid, token);
        if (patient is null) return null;
        if (patient.PatientUid != patientUid) throw new UnauthorizedAccessException("Patient context is unavailable.");
        await readAudit.RecordOpenedAsync(patientUid, correlation, token);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - patient.DateOfBirth.Year;
        if (patient.DateOfBirth > today.AddYears(-age)) age--;
        var data = new PatientCppSummaryResponse(patientUid,
            new(patient.FullName, patient.PreferredName, patient.DateOfBirth, age, patient.SexAtBirth, patient.GenderIdentity, patient.PhoneNumber),
            PatientCppSection<PatientCppProblem>.From([],0), PatientCppSection<PatientCppAllergy>.From([],0),
            PatientCppSection<PatientCppMedication>.From([],0), PatientCppSection<PatientCppPrescription>.From([],0),
            PatientCppSection<PatientCppImmunization>.From([],0), PatientCppSection<PatientCppResult>.From([],0),
            PatientCppSection<PatientCppVitals>.From([],0), PatientCppSection<PatientCppEncounter>.From([],0),
            PatientCppSection<PatientCppReferral>.From([],0), PatientCppSection<PatientCppDocument>.From([],0));
        foreach (var category in CppDisplayCatalog.Categories.Where(x => categories.Contains(x.Key)))
        {
            if(category.Key=="Problems")
            {
                var detail=PatientCppSection<PatientCppProblemPrint>.Unavailable();
                var compact=await LoadProblems(patientUid,correlation,token,true,rows => detail=PatientCppSection<PatientCppProblemPrint>.From(
                    rows.OrderByDescending(x=>x.EffectiveOnset?.Year).ThenByDescending(x=>x.EffectiveOnset?.Month).ThenByDescending(x=>x.EffectiveOnset?.Day).ThenBy(x=>x.PatientProblemUid)
                    .Select(x=>new PatientCppProblemPrint(x.ProblemName,x.ProblemDescription,x.ProblemStatus,x.EffectiveOnset?.Display ?? "Not recorded",x.ClinicalFields?.LifeStage ?? "Not recorded",x.ClinicalFields?.Notes)).ToArray(),rows.Count));
                data=data with {Problems=compact,ProblemsForPrint=detail};
                continue;
            }
            if(category.Key=="Immunizations")
            {
                var details=PatientCppSection<PatientCppImmunizationPrint>.Unavailable();
                var compact=await LoadImmunizations(patientUid,correlation,token,true,rows=>details=PatientCppSection<PatientCppImmunizationPrint>.From(
                    rows.Select(x=>new PatientCppImmunizationPrint(x.VaccineName,x.Status=="Refused"?"Refused":"Administered",x.AdministrationDisplay,x.RefusalDisplay,
                    x.ClinicalFields?.Refused is {} refused ? (refused?"Yes":"No") : "Not recorded",x.ClinicalFields?.DIN,x.ClinicalFields?.Manufacturer,x.ClinicalFields?.ImmunizationType ?? "Not recorded",
                    x.DoseNumber,x.Route,x.Site,x.LotNumber,x.SourceType,x.SourceDescription,x.AdministeredByName,x.Notes)).ToArray(),rows.Count));
                data=data with {Immunizations=compact,ImmunizationsForPrint=details};
                continue;
            }
            if(category.Key=="Allergies")
            {
                var detail=PatientCppSection<PatientCppAllergyPrint>.Unavailable();
                var compact=await LoadAllergies(patientUid,correlation,token,true,snapshot=>
                {
                    var rows=snapshot.Items.Where(x=>x.Status=="Active").ToArray();
                    detail=snapshot.Documentation.State==PatientCppSectionStates.ExplicitlyNone?PatientCppSection<PatientCppAllergyPrint>.ExplicitlyNone():PatientCppSection<PatientCppAllergyPrint>.From(
                        rows.Select(x=>new PatientCppAllergyPrint(x.AllergenName,x.AllergenType,x.Status,x.Reaction,x.Severity,x.OnsetDisplay,x.ClinicalFields?.LifeStage??"Not recorded",x.Notes,x.CreatedAt.ToString("yyyy-MM-dd HH:mm 'UTC'",System.Globalization.CultureInfo.InvariantCulture))).ToArray(),rows.Length);
                });
                data=data with {Allergies=compact,AllergiesForPrint=detail};continue;
            }
            data = category.Key switch
            {
                "Alerts" => data with { Alerts = await LoadAlerts(patientUid,correlation,token,true) },
                "RiskFactors" => data with { RiskFactors = await LoadRiskFactors(patientUid,correlation,token,true) },
                "FamilyHistory" => data with { FamilyHistory = await LoadFamilyHistory(patientUid,correlation,token,true) },
                "Medications" => data with { Medications = await LoadMedications(patientUid,correlation,token,true,medicationScope) },
                "Prescriptions" => data with { Prescriptions = await LoadPrescriptions(patientUid,correlation,token,true) },
                "Results" => data with { Results = await LoadResults(patientUid,correlation,token,true) },
                "Vitals" => data with { Vitals = await LoadVitals(patientUid,correlation,token,true) },
                "Encounters" => data with { Encounters = await LoadEncounters(patientUid,correlation,token,true) },
                "Referrals" => data with { Referrals = await LoadReferrals(patientUid,correlation,token,true) },
                "Documents" => data with { Documents = await LoadDocuments(patientUid,correlation,token,true) },
                _ => data // History is loaded from its existing authoritative service by the print composer.
            };
        }
        return data;
    }

    private static void EnsureOwned<T>(IEnumerable<T> rows, Guid patientUid)
    {
        var property = typeof(T).GetProperty("PatientUid")
            ?? throw new InvalidOperationException("CPP source ownership cannot be established.");
        if (rows.Any(x => property.GetValue(x) is not Guid uid || uid != patientUid))
            throw new UnauthorizedAccessException("CPP source ownership is invalid.");
    }
}
