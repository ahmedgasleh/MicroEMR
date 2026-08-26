namespace MicroEMR.Application.PatientCpp;

public static class PatientCppSectionStates
{
    public const string HasEntries = "HasEntries";
    public const string NotDocumented = "NotDocumented";
    public const string NotAuthorized = "NotAuthorized";
    public const string Unavailable = "Unavailable";
    public const string ExplicitlyNone = "ExplicitlyNone";
}

public sealed record PatientCppSection<T>(
    string State,
    IReadOnlyList<T> Items,
    int? TotalCount)
{
    public static PatientCppSection<T> From(IReadOnlyList<T> items, int totalCount) =>
        new(items.Count == 0 ? PatientCppSectionStates.NotDocumented : PatientCppSectionStates.HasEntries,
            items, totalCount);

    public static PatientCppSection<T> NotAuthorized() =>
        new(PatientCppSectionStates.NotAuthorized, [], null);

    public static PatientCppSection<T> Unavailable() =>
        new(PatientCppSectionStates.Unavailable, [], null);
}

public sealed record PatientCppDemographics(
    string DisplayName,
    string? PreferredName,
    DateOnly DateOfBirth,
    int Age,
    string? SexAtBirth,
    string? GenderIdentity,
    string? PreferredContact);

public sealed record PatientCppProblem(Guid ProblemUid, string DisplayName, string Status, DateTime? OnsetDate);
public sealed record PatientCppAllergy(Guid AllergyUid, string DisplayName, string Status, string? Reaction, string? Severity);
public sealed record PatientCppMedication(Guid MedicationUid, string DisplayName, string? Strength, string? Route, string? Frequency, DateTime? StartDate);
public sealed record PatientCppPrescription(Guid PrescriptionUid, string DisplayName, DateOnly PrescribedDate, string Directions);
public sealed record PatientCppImmunization(Guid ImmunizationUid, string VaccineName, DateOnly AdministrationDate, string SourceType);
public sealed record PatientCppResult(Guid ResultUid, string Name, string Type, DateTime ResultDate, string? Value, string? Unit, string Abnormality, string ReviewStatus, string Provenance);
public sealed record PatientCppVitals(Guid VitalUid, DateTime RecordedAt, int? Systolic, int? Diastolic, int? HeartRate, decimal? WeightKg, decimal? Bmi, int? OxygenSaturation);
public sealed record PatientCppEncounter(Guid EncounterUid, DateTime EncounterDateUtc, string Type, string? ProviderName, string? ReasonForVisit);
public sealed record PatientCppReferral(Guid ReferralUid, string RecipientName, string? RecipientOrganization, string Status, DateTime CreatedAtUtc);
public sealed record PatientCppDocument(Guid DocumentUid, string Title, string DocumentType, string Status, DateTime CreatedAtUtc);

public sealed record PatientCppSummaryResponse(
    Guid PatientUid,
    PatientCppDemographics Demographics,
    PatientCppSection<PatientCppProblem> Problems,
    PatientCppSection<PatientCppAllergy> Allergies,
    PatientCppSection<PatientCppMedication> Medications,
    PatientCppSection<PatientCppPrescription> Prescriptions,
    PatientCppSection<PatientCppImmunization> Immunizations,
    PatientCppSection<PatientCppResult> Results,
    PatientCppSection<PatientCppVitals> Vitals,
    PatientCppSection<PatientCppEncounter> Encounters,
    PatientCppSection<PatientCppReferral> Referrals,
    PatientCppSection<PatientCppDocument> Documents);

