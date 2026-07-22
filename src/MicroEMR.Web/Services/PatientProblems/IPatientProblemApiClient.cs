using MicroEMR.Web.Models.PatientProblems;

namespace MicroEMR.Web.Services.PatientProblems;

public interface IPatientProblemApiClient
{
    Task<IReadOnlyList<PatientProblemViewModel>> GetByPatientUidAsync(Guid patientUid, string statusFilter, CancellationToken cancellationToken = default);
    Task<PatientProblemViewModel?> GetByUidAsync(Guid patientUid, Guid patientProblemUid, CancellationToken cancellationToken = default);
    Task<PatientProblemViewModel> CreateAsync(Guid patientUid, CreatePatientProblemRequest request, CancellationToken cancellationToken = default);
    Task<PatientProblemViewModel?> UpdateAsync(Guid patientUid, Guid patientProblemUid, UpdatePatientProblemRequest request, CancellationToken cancellationToken = default);
    Task<PatientProblemViewModel?> ResolveAsync(Guid patientUid, Guid patientProblemUid, ResolvePatientProblemRequest request, CancellationToken cancellationToken = default);
}
