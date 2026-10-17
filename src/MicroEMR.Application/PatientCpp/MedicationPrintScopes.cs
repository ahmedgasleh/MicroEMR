using MicroEMR.Core.PatientMedications;
namespace MicroEMR.Application.PatientCpp;

public static class MedicationPrintScopes
{
    public const string AllActive = "AllActive";
    public static void Validate(string? scope)
    {
        if (scope is not (MedicationClassifications.Ongoing or MedicationClassifications.Acute or MedicationClassifications.Unclassified or AllActive))
            throw new ArgumentException("Select a supported medication print scope.");
    }
    public static string Label(string scope) => scope == AllActive ? "All active" : scope;
    public static bool Includes(string scope, string? classification) => scope == AllActive || scope == MedicationClassifications.Label(classification);
}
