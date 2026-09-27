namespace MicroEMR.Application.Patients.Contracts;

public sealed record RegisterPatientRequest(
    CreatePatientRequest Patient,
    Guid? ReferringProviderUid,
    Guid? AttendingProviderUid,
    Guid? PrimaryCareProviderUid);

public sealed record RegisterPatientResult(
    PatientDetailsResponse Patient,
    IReadOnlyList<string> FailedCareTeamRoles);
