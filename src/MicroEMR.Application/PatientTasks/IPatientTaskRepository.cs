namespace MicroEMR.Application.PatientTasks;

public interface IPatientTaskRepository
{
    Task<IReadOnlyList<PatientTaskResponse>> GetByPatientUidAsync(Guid patientUid, string statusFilter, CancellationToken cancellationToken = default);
    Task<PatientTaskResponse?> GetByUidAsync(Guid patientUid, Guid patientTaskUid, CancellationToken cancellationToken = default);
    Task<PatientTaskResponse?> CreateAsync(Guid patientUid, CreatePatientTaskRequest request, long? userId, CancellationToken cancellationToken = default);
    Task<PatientTaskResponse?> UpdateAsync(Guid patientUid, Guid patientTaskUid, UpdatePatientTaskRequest request, long? userId, CancellationToken cancellationToken = default);
    Task<PatientTaskResponse?> CompleteAsync(Guid patientUid, Guid patientTaskUid, CompletePatientTaskRequest request, long? userId, CancellationToken cancellationToken = default);
    Task<PatientTaskResponse?> ReopenAsync(Guid patientUid, Guid patientTaskUid, long? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PatientDashboardTaskResponse>> GetOpenForDashboardAsync(long? assignedTo, int maxRows, CancellationToken cancellationToken = default);
}
