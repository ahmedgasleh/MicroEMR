using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Repositories;

namespace MicroEMR.Application.PatientEncounters.Services;

public sealed class PatientEncounterService : IPatientEncounterService
{
    private readonly IPatientEncounterRepository _repository;

    public PatientEncounterService(
        IPatientEncounterRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<PatientEncounterListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        return _repository.GetByPatientUidAsync(
            patientUid,
            cancellationToken);
    }

    public Task<PatientEncounterDetailsResponse?> GetByUidAsync(
        Guid encounterUid,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByUidAsync(
            encounterUid,
            cancellationToken);
    }

    public Task<PatientEncounterDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientEncounterRequest request,
        long? createdBy,
        string? createdByDisplayName,
        CancellationToken cancellationToken = default)
    {
        return _repository.CreateAsync(
            patientUid,
            request,
            createdBy,
            createdByDisplayName,
            cancellationToken);
    }
}
