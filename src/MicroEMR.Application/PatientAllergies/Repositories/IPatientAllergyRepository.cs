using MicroEMR.Application.PatientAllergies.Contracts;

namespace MicroEMR.Application.PatientAllergies.Repositories;

public interface IPatientAllergyRepository
{
    Task<IReadOnlyList<PatientAllergyListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default);

    Task<AllergyDocumentationStateResponse> GetDocumentationStateAsync(Guid patientUid, CancellationToken cancellationToken = default);
    Task<NoKnownAllergiesAssertionResponse> AssertNoKnownAllergiesAsync(Guid patientUid, long verifiedBy, CancellationToken cancellationToken = default);
    Task<NoKnownAllergiesAssertionResponse?> RevokeNoKnownAllergiesAsync(Guid patientUid, RevokeNoKnownAllergiesRequest request, long revokedBy, CancellationToken cancellationToken = default);

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
