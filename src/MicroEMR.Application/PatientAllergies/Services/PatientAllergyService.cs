using MicroEMR.Application.PatientAllergies.Contracts;
using MicroEMR.Application.PatientAllergies.Repositories;

namespace MicroEMR.Application.PatientAllergies.Services;

public sealed class PatientAllergyService : IPatientAllergyService
{
    private readonly IPatientAllergyRepository _repository;

    public PatientAllergyService(
        IPatientAllergyRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<PatientAllergyListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        return _repository.GetByPatientUidAsync(
            patientUid,
            cancellationToken);
    }

    public Task<PatientAllergyDetailsResponse?> GetByUidAsync(
        Guid allergyUid,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByUidAsync(
            allergyUid,
            cancellationToken);
    }

    public Task<PatientAllergyDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientAllergyRequest request,
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

    public Task<PatientAllergyDetailsResponse?> UpdateAsync(
        Guid patientUid,
        Guid allergyUid,
        UpdatePatientAllergyRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _repository.UpdateAsync(
            patientUid, allergyUid, request, updatedBy, cancellationToken);
    }
    public Task<PatientAllergyDetailsResponse?> ResolveAsync(Guid patientUid, Guid allergyUid,
        ResolvePatientAllergyRequest request, long? resolvedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _repository.ResolveAsync(patientUid, allergyUid, request, resolvedBy, cancellationToken);
    }
}
