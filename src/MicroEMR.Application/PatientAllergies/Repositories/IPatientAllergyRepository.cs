using MicroEMR.Application.PatientAllergies.Contracts;

namespace MicroEMR.Application.PatientAllergies.Repositories;

public interface IPatientAllergyRepository
{
    Task<AllergySnapshot> GetSnapshotAsync(Guid patientUid,CancellationToken token=default) => throw new NotSupportedException("A coherent allergy snapshot is required.");
    async Task<PatientAllergyDetailsResponse?> GetForPatientAsync(Guid patientUid,Guid allergyUid,CancellationToken token=default)
    {
        var row=await GetByUidAsync(allergyUid,token);
        return row?.PatientUid==patientUid ? row : null;
    }
    Task<IReadOnlyList<PatientAllergyListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default);

    Task<AllergyDocumentationStateResponse> GetDocumentationStateAsync(Guid patientUid, CancellationToken cancellationToken = default);
    Task<NoKnownAllergiesAssertionResponse> AssertNoKnownAllergiesAsync(Guid patientUid, long verifiedBy, CancellationToken cancellationToken = default, string? correlation = null);
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
