using MicroEMR.Web.Models.PatientMedications;

namespace MicroEMR.Web.Services.PatientMedications;

public interface IPatientMedicationApiClient
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
        CancellationToken cancellationToken = default);
    Task<PatientMedicationDetailsResponse?> UpdateAsync(Guid patientUid, Guid medicationUid,
        UpdatePatientMedicationRequest request, CancellationToken cancellationToken = default);
}
