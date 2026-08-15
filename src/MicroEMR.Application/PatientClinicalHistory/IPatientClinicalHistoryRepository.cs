namespace MicroEMR.Application.PatientClinicalHistory;

public interface IPatientClinicalHistoryRepository
{
    Task<IReadOnlyList<PatientClinicalHistoryResponse>> ListAsync(Guid patientUid, string status, CancellationToken cancellationToken = default);
    Task<PatientClinicalHistoryResponse> CreateAsync(Guid patientUid, CreatePatientClinicalHistoryRequest request, long actor, CancellationToken cancellationToken = default);
    Task<PatientClinicalHistoryResponse?> UpdateAsync(Guid patientUid, Guid historyUid, UpdatePatientClinicalHistoryRequest request, long actor, CancellationToken cancellationToken = default);
    Task<PatientClinicalHistoryResponse?> ArchiveAsync(Guid patientUid, Guid historyUid, string rowVersion, long actor, CancellationToken cancellationToken = default);
}

public interface IPatientClinicalHistoryService : IPatientClinicalHistoryRepository;
