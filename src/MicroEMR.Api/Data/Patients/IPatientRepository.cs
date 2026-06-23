using MicroEMR.Api.Contracts.Patients;

namespace MicroEMR.Api.Data.Patients;

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
}