using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Application.PatientCpp;

public sealed partial class PatientCppService
{
    public async Task<PatientCppSummaryResponse?> GetForPrintAsync(Guid patientUid, IReadOnlyList<string> categories,
        string correlation, CancellationToken token = default)
    {
        CppPrintSelection.Validate(categories);
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
            data = category.Key switch
            {
                "Problems" => data with { Problems = await LoadProblems(patientUid,correlation,token,true) },
                "Allergies" => data with { Allergies = await LoadAllergies(patientUid,correlation,token,true) },
                "Medications" => data with { Medications = await LoadMedications(patientUid,correlation,token,true) },
                "Prescriptions" => data with { Prescriptions = await LoadPrescriptions(patientUid,correlation,token,true) },
                "Immunizations" => data with { Immunizations = await LoadImmunizations(patientUid,correlation,token,true) },
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
