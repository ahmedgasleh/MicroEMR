using MicroEMR.Api.Contracts.Patients;
using MicroEMR.Api.Data.Patients;

namespace MicroEMR.Api.Services.Patients;

public sealed class PatientService : IPatientService
{
    private readonly IPatientRepository _patientRepository;

    public PatientService (
        IPatientRepository patientRepository )
    {
        _patientRepository = patientRepository;
    }

    public Task<PatientSearchResponse> SearchAsync (
        string? searchText,
        DateOnly? dateOfBirth,
        int pageNumber,
        int pageSize,
        bool includeInactive,
        CancellationToken cancellationToken = default )
    {
        return _patientRepository.SearchAsync(
            searchText,
            dateOfBirth,
            pageNumber,
            pageSize,
            includeInactive,
            cancellationToken);
    }

    public Task<PatientDetailsResponse?> GetByUidAsync (
        Guid patientUid,
        CancellationToken cancellationToken = default )
    {
        return _patientRepository.GetByUidAsync(
            patientUid,
            cancellationToken);
    }

    public Task<PatientDetailsResponse> CreateAsync (
        CreatePatientRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default )
    {
        if (request.DateOfBirth.HasValue &&
            request.DateOfBirth.Value >
            DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException(
                "Date of birth cannot be in the future.");
        }

        return _patientRepository.CreateAsync(
            request,
            createdBy,
            cancellationToken);
    }

    public Task<PatientDetailsResponse?> UpdateDemographicsAsync(
        Guid patientUid,
        UpdatePatientDemographicsRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default)
    {
        if (request.DateOfBirth.HasValue &&
            request.DateOfBirth.Value >
            DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException(
                "Date of birth cannot be in the future.");
        }

        return _patientRepository.UpdateDemographicsAsync(
            patientUid,
            request,
            updatedBy,
            cancellationToken);
    }
}
