using MicroEMR.Application.PatientEncounters.Contracts;

namespace MicroEMR.Application.PatientEncounters;

public static class EncounterSigningValidator
{
    public static IReadOnlyList<string> Validate(PatientEncounterDetailsResponse encounter)
    {
        var errors = new List<string>();
        if (!EncounterStatuses.IsEditable(encounter.Status))
            errors.Add("Only a draft encounter can be signed.");
        if (encounter.EncounterDateUtc == default)
            errors.Add("Encounter date is required before signing.");
        if (string.IsNullOrWhiteSpace(encounter.EncounterType))
            errors.Add("Encounter type is required before signing.");
        if (string.IsNullOrWhiteSpace(encounter.ProviderName))
            errors.Add("A responsible provider is required before signing.");
        if (string.IsNullOrWhiteSpace(encounter.ReasonForVisit))
            errors.Add("A reason for visit is required before signing.");
        if (new[] { encounter.Notes, encounter.SubjectiveNote, encounter.ObjectiveNote,
                    encounter.AssessmentNote, encounter.PlanNote }.All(string.IsNullOrWhiteSpace))
            errors.Add("A clinical note is required before signing.");
        return errors;
    }
}
