using MicroEMR.Application.PatientCareTeam;
using MicroEMR.Application.Patients.Contracts;
using Microsoft.Extensions.Logging;

namespace MicroEMR.Application.Patients.Services;

public interface IPatientRegistrationService
{
    Task<RegisterPatientResult> RegisterAsync(
        RegisterPatientRequest request, long actorUserId, CancellationToken cancellationToken = default);
}

public sealed class PatientRegistrationService(
    IPatientService patients,
    IPatientCareTeamService careTeam,
    ILogger<PatientRegistrationService> logger) : IPatientRegistrationService
{
    public async Task<RegisterPatientResult> RegisterAsync(
        RegisterPatientRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Patient);

        var patient = await patients.CreateAsync(request.Patient, actorUserId, cancellationToken);
        var assignments = new (Guid? ProviderUid, string Code, string Label)[]
        {
            (request.ReferringProviderUid, "REFERRING", "Referring Physician"),
            (request.AttendingProviderUid, "ATTENDING", "Attending Physician"),
            (request.PrimaryCareProviderUid, "PCP", "Primary Care Physician")
        };
        var failures = new List<string>();
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var (providerUid, code, label) in assignments)
        {
            if (!providerUid.HasValue) continue;
            try
            {
                await careTeam.AddAsync(patient.PatientUid,
                    new AddPatientCareTeamRelationshipRequest(providerUid.Value, code, true, startDate),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception,
                    "Patient {PatientUid} was registered, but care-team role {RoleCode} could not be assigned.",
                    patient.PatientUid, code);
                failures.Add(label);
            }
        }

        return new RegisterPatientResult(patient, failures);
    }
}
