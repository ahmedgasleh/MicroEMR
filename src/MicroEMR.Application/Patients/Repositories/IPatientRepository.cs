using MicroEMR.Application.Patients.Contracts;

namespace MicroEMR.Application.Patients.Repositories;

public interface IPatientRepository
{
    Task<PatientSearchResponse> SearchAsync (
        string? searchText,
        DateOnly? dateOfBirth,
        int pageNumber,
        int pageSize,
        bool includeInactive,
        CancellationToken cancellationToken = default );

    Task<PatientDetailsResponse?> GetByUidAsync (
        Guid patientUid,
        CancellationToken cancellationToken = default );

    Task<PatientDetailsResponse> CreateAsync (
        CreatePatientRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default );

    Task<PatientDetailsResponse?> UpdateDemographicsAsync(
        Guid patientUid,
        UpdatePatientDemographicsRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default);
}
