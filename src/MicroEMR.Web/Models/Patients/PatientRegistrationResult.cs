namespace MicroEMR.Web.Models.Patients;

public sealed record PatientRegistrationResult(
    PatientDetailsResponse Patient,
    IReadOnlyList<string> FailedCareTeamRoles);
