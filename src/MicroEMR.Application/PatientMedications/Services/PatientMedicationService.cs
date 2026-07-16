using MicroEMR.Application.PatientMedications.Contracts;
using MicroEMR.Application.PatientMedications.Repositories;

namespace MicroEMR.Application.PatientMedications.Services;

public sealed class PatientMedicationService : IPatientMedicationService
{
    private readonly IPatientMedicationRepository _repository;

    public PatientMedicationService(
        IPatientMedicationRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<PatientMedicationListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        return _repository.GetByPatientUidAsync(
            patientUid,
            cancellationToken);
    }

    public Task<PatientMedicationDetailsResponse?> GetByUidAsync(
        Guid medicationUid,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByUidAsync(
            medicationUid,
            cancellationToken);
    }

    public Task<PatientMedicationDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientMedicationRequest request,
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

    public Task<PatientMedicationDetailsResponse?> UpdateAsync(
        Guid patientUid, Guid medicationUid, UpdatePatientMedicationRequest request,
        long? updatedBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _repository.UpdateAsync(patientUid, medicationUid, request, updatedBy, cancellationToken);
    }
    public Task<PatientMedicationDetailsResponse?> DiscontinueAsync(
        Guid patientUid, Guid medicationUid, DiscontinuePatientMedicationRequest request,
        long? discontinuedBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _repository.DiscontinueAsync(patientUid, medicationUid, request, discontinuedBy, cancellationToken);
    }
}
