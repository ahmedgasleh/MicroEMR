namespace MicroEMR.Application.PatientReferrals;

public static class ReferralClinicalSelectionKinds
{
    public const string Cpp = "CPP";
    public const string Encounter = "ENCOUNTER";
    public const string Result = "RESULT";
    public const string File = "FILE";
}

public static class ReferralCppCategoryCodes
{
    public const string Problems = "PROBLEMS";
    public const string Allergies = "ALLERGIES";
    public const string Medications = "MEDICATIONS";
}

public sealed record ReferralClinicalSelectionInput(
    string SelectionKind, string? CppCategoryCode = null, Guid? EncounterUid = null, Guid? ResultUid = null, Guid? FileUid = null);

public sealed class ReplacePatientReferralClinicalSelectionsRequest
{
    public required string RowVersion { get; init; }
    public IReadOnlyList<ReferralClinicalSelectionInput> Selections { get; init; } = [];
}

public sealed record PatientReferralClinicalSelectionResponse(
    Guid SelectionUid, string SelectionKind, string? CppCategoryCode, Guid? EncounterUid, Guid? ResultUid,
    DateTime CreatedAt, long CreatedBy, Guid? FileUid = null);

public sealed record PatientReferralClinicalSelectionsResponse(
    Guid PatientUid, Guid ReferralUid, string RowVersion,
    IReadOnlyList<PatientReferralClinicalSelectionResponse> Selections);

public sealed class ReferralClinicalSelectionRuleException(string message) : InvalidOperationException(message);
