using MicroEMR.Application.PatientProblems.Contracts;

namespace MicroEMR.Application.PatientProblems.Services;

public interface IPatientProblemService
{
    Task<IReadOnlyList<PatientProblemResponse>> GetByPatientUidAsync(Guid patientUid, string statusFilter, CancellationToken cancellationToken = default);
    Task<PatientProblemResponse?> GetByUidAsync(Guid patientUid, Guid patientProblemUid, CancellationToken cancellationToken = default);
    Task<PatientProblemResponse> CreateAsync(Guid patientUid, CreatePatientProblemRequest request, long? createdBy, CancellationToken cancellationToken = default);
    Task<PatientProblemResponse?> UpdateAsync(Guid patientUid, Guid patientProblemUid, UpdatePatientProblemRequest request, long? updatedBy, CancellationToken cancellationToken = default);
    Task<PatientProblemResponse?> ResolveAsync(Guid patientUid, Guid patientProblemUid, ResolvePatientProblemRequest request, long? resolvedBy, CancellationToken cancellationToken = default);
}
