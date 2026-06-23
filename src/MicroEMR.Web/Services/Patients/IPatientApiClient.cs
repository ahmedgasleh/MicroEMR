using MicroEMR.Web.Models.Patients;

namespace MicroEMR.Web.Services.Patients;

public interface IPatientApiClient
{
    Task<PatientSearchResponse> SearchAsync (
        string? searchText,
        DateOnly? dateOfBirth,
        int pageNumber = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default );

    Task<PatientDetailsResponse?> GetByUidAsync (
        Guid patientUid,
        CancellationToken cancellationToken = default );

    Task<PatientDetailsResponse> CreateAsync (
        CreatePatientRequest request,
        CancellationToken cancellationToken = default );
}