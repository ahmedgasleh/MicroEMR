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

    public Task<AllergySnapshot> GetSnapshotAsync(Guid patientUid,CancellationToken token=default)=>_repository.GetSnapshotAsync(patientUid,token);
    public Task<PatientAllergyDetailsResponse?> GetForPatientAsync(Guid patientUid,Guid allergyUid,CancellationToken token=default)=>_repository.GetForPatientAsync(patientUid,allergyUid,token);
    public Task<IReadOnlyList<PatientAllergyListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        return _repository.GetByPatientUidAsync(
            patientUid,
            cancellationToken);
    }

    public Task<AllergyDocumentationStateResponse> GetDocumentationStateAsync(Guid patientUid, CancellationToken cancellationToken = default) =>
        _repository.GetDocumentationStateAsync(patientUid, cancellationToken);

    public Task<NoKnownAllergiesAssertionResponse> AssertNoKnownAllergiesAsync(Guid patientUid, long verifiedBy, CancellationToken cancellationToken = default, string? correlation = null) =>
        _repository.AssertNoKnownAllergiesAsync(patientUid, verifiedBy, cancellationToken, correlation);

    public Task<NoKnownAllergiesAssertionResponse?> RevokeNoKnownAllergiesAsync(Guid patientUid, RevokeNoKnownAllergiesRequest request, long revokedBy, CancellationToken cancellationToken = default) =>
        _repository.RevokeNoKnownAllergiesAsync(patientUid, request, revokedBy, cancellationToken);

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
        Validate(request);
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
        Validate(request);
        return _repository.UpdateAsync(
            patientUid, allergyUid, request, updatedBy, cancellationToken);
    }
    public Task<PatientAllergyDetailsResponse?> ResolveAsync(Guid patientUid, Guid allergyUid,
        ResolvePatientAllergyRequest request, long? resolvedBy,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        return _repository.ResolveAsync(patientUid, allergyUid, request, resolvedBy, cancellationToken);
    }
    private static void Validate(object request)
    {
        var errors=new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if(!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request,new(request),errors,true))
            throw new PatientAllergyValidationException(errors[0].ErrorMessage ?? "Invalid allergy.");
    }
}
