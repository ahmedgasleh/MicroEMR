using MicroEMR.Web.Models.PatientAllergies;

namespace MicroEMR.Web.Services.PatientAllergies;

public interface IPatientAllergyApiClient
{
    Task<IReadOnlyList<PatientAllergyListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default);

    Task<AllergyDocumentationStateResponse?> GetDocumentationStateAsync(Guid patientUid, CancellationToken cancellationToken = default);
    Task<NoKnownAllergiesAssertionResponse> AssertNoKnownAllergiesAsync(Guid patientUid, CancellationToken cancellationToken = default);
    Task<NoKnownAllergiesAssertionResponse?> RevokeNoKnownAllergiesAsync(Guid patientUid, RevokeNoKnownAllergiesRequest request, CancellationToken cancellationToken = default);

    Task<PatientAllergyDetailsResponse?> GetByUidAsync(
        Guid allergyUid,
        CancellationToken cancellationToken = default);

    Task<PatientAllergyDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientAllergyRequest request,
        CancellationToken cancellationToken = default);

    Task<PatientAllergyDetailsResponse?> UpdateAsync(
        Guid patientUid,
        Guid allergyUid,
        UpdatePatientAllergyRequest request,
        CancellationToken cancellationToken = default);
    Task<PatientAllergyDetailsResponse?> ResolveAsync(Guid patientUid, Guid allergyUid,
        ResolvePatientAllergyRequest request, CancellationToken cancellationToken = default);
}
