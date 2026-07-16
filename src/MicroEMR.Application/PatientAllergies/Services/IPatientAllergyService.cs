using MicroEMR.Application.PatientAllergies.Contracts;

namespace MicroEMR.Application.PatientAllergies.Services;

public interface IPatientAllergyService
{
    Task<IReadOnlyList<PatientAllergyListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default);

    Task<PatientAllergyDetailsResponse?> GetByUidAsync(
        Guid allergyUid,
        CancellationToken cancellationToken = default);

    Task<PatientAllergyDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientAllergyRequest request,
        long? createdBy,
        string? createdByDisplayName,
        CancellationToken cancellationToken = default);

    Task<PatientAllergyDetailsResponse?> UpdateAsync(
        Guid patientUid,
        Guid allergyUid,
        UpdatePatientAllergyRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default);
    Task<PatientAllergyDetailsResponse?> ResolveAsync(Guid patientUid, Guid allergyUid,
        ResolvePatientAllergyRequest request, long? resolvedBy,
        CancellationToken cancellationToken = default);
}
