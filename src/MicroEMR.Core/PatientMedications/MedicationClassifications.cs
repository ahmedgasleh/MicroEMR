namespace MicroEMR.Core.PatientMedications;

public static class MedicationClassifications
{
    public const string Ongoing = "Ongoing";
    public const string Acute = "Acute";
    public const string Unclassified = "Unclassified";
    public static bool IsValid(string? value) => string.IsNullOrWhiteSpace(value) || value is Ongoing or Acute or Unclassified;
    public static string? Normalize(string? value) => IsValid(value)
        ? (string.IsNullOrWhiteSpace(value) || value == Unclassified ? null : value)
        : throw new ArgumentException("Select Ongoing, Acute or Unclassified.", nameof(value));
    public static string Label(string? value) => Normalize(value) ?? Unclassified;
}
