namespace MicroEMR.Application.PatientClinicalHistory;

public sealed class PatientClinicalHistoryService(IPatientClinicalHistoryRepository repository) : IPatientClinicalHistoryService
{
    public Task<IReadOnlyList<PatientClinicalHistoryResponse>> ListAsync(Guid patientUid, string status, CancellationToken cancellationToken = default) => repository.ListAsync(patientUid, status is "Archived" or "All" ? status : "Active", cancellationToken);
    public Task<PatientClinicalHistoryResponse> CreateAsync(Guid patientUid, CreatePatientClinicalHistoryRequest request, long actor, CancellationToken cancellationToken = default) => repository.CreateAsync(patientUid, request, actor, cancellationToken);
    public Task<PatientClinicalHistoryResponse?> UpdateAsync(Guid patientUid, Guid historyUid, UpdatePatientClinicalHistoryRequest request, long actor, CancellationToken cancellationToken = default) => repository.UpdateAsync(patientUid, historyUid, request, actor, cancellationToken);
    public Task<PatientClinicalHistoryResponse?> ArchiveAsync(Guid patientUid, Guid historyUid, string rowVersion, long actor, CancellationToken cancellationToken = default) => repository.ArchiveAsync(patientUid, historyUid, rowVersion, actor, cancellationToken);
}
