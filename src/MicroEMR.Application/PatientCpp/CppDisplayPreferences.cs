using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientCpp;

public sealed record CppDisplayField(string Key, string Label);
public sealed record CppDisplayCategory(string Key, string Label, IReadOnlyList<CppDisplayField> Fields);

// Stable presentation keys for the existing summary; not a clinical data model.
public static class CppDisplayCatalog
{
    public static IReadOnlyList<CppDisplayCategory> Categories { get; } = Array.AsReadOnly(new[]
    {
        Category("Problems", "Active Problems", ("DisplayName", "Problem"), ("OnsetDate", "Onset date"), ("AdditionalCount", "Additional record count")),
        Category("Allergies", "Active Allergies", ("DisplayName", "Allergen"), ("Reaction", "Reaction"), ("Severity", "Severity")),
        Category("Medications", "Active Medications", ("DisplayName", "Medication"), ("Strength", "Strength"), ("Frequency", "Frequency"), ("Route", "Route")),
        Category("Prescriptions", "Current Prescriptions", ("DisplayName", "Prescription"), ("PrescribedDate", "Prescribed date"), ("Directions", "Directions")),
        Category("Results", "Recent Results", ("Name", "Result name"), ("Abnormality", "Recorded abnormality"), ("ResultDate", "Result date"), ("ReviewStatus", "Review status"), ("Provenance", "Provenance")),
        Category("Vitals", "Latest Vitals", ("RecordedAt", "Recorded date/time"), ("BloodPressure", "Blood pressure"), ("HeartRate", "Heart rate"), ("WeightBmi", "Weight / BMI"), ("OxygenSaturation", "SpO2")),
        Category("Immunizations", "Recent Immunizations", ("VaccineName", "Vaccine"), ("AdministrationDate", "Administration date"), ("SourceType", "Source")),
        Category("Encounters", "Latest Signed Encounter", ("Type", "Encounter type"), ("EncounterDate", "Encounter date"), ("ProviderOrReason", "Provider / reason")),
        Category("Referrals", "Referrals", ("RecipientName", "Recipient"), ("Status", "Status"), ("OpenCount", "Open referral count")),
        Category("Documents", "Recent Documents", ("Title", "Title"), ("DocumentType", "Document type"), ("CreatedAt", "Created date")),
        Category("History", "Past Medical and Surgical History", ("HistoryType", "History type"), ("Description", "Description"), ("RelevantDate", "Relevant date"))
    });

    private static CppDisplayCategory Category(string key, string label, params (string Key, string Label)[] fields) =>
        new(key, label, Array.AsReadOnly(fields.Select(x => new CppDisplayField($"{key}.{x.Key}", x.Label)).ToArray()));
}

public sealed class SaveCppDisplayPreferencesRequest
{
    [Required] public List<string> HiddenCategories { get; set; } = [];
    [Required] public List<string> HiddenFields { get; set; } = [];
    [StringLength(12)] public string? RowVersion { get; set; }
}

public sealed record CppDisplayPreferencesResponse(
    IReadOnlyList<string> HiddenCategories,
    IReadOnlyList<string> HiddenFields,
    string? RowVersion,
    bool IsAvailable = true);

public sealed record CppDisplayPreferencesData(string SettingsJson, string RowVersion);

public interface ICppDisplayPreferencesRepository
{
    Task<CppDisplayPreferencesData?> GetAsync(long userId, CancellationToken cancellationToken = default);
    Task<CppDisplayPreferencesData> SaveAsync(long userId, string settingsJson, string? expectedRowVersion,
        CancellationToken cancellationToken = default);
}

public sealed class CppDisplayPreferencesConcurrencyException() : Exception("CPP display preferences changed. Reload before saving.");
