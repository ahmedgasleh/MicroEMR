using MicroEMR.Application.PatientMedications.Contracts;

namespace MicroEMR.Application.PatientMedications.Repositories;

public interface IPatientMedicationRepository
{
    Task<IReadOnlyList<PatientMedicationListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default);

    Task<PatientMedicationDetailsResponse?> GetByUidAsync(
        Guid medicationUid,
        CancellationToken cancellationToken = default);

    Task<PatientMedicationDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientMedicationRequest request,
        long? createdBy,
        string? createdByDisplayName,
        CancellationToken cancellationToken = default);

    Task<PatientMedicationDetailsResponse?> UpdateAsync(
        Guid patientUid, Guid medicationUid, UpdatePatientMedicationRequest request,
        long? updatedBy, CancellationToken cancellationToken = default);
}
