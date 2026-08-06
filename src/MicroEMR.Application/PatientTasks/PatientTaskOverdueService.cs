using MicroEMR.Application.ClinicalUsers;

namespace MicroEMR.Application.PatientTasks;

public interface IPatientTaskOverdueService
{
    Task<int> GetOverdueCountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OverduePatientTaskItem>> GetOverdueAsync(CancellationToken cancellationToken = default);
}

public sealed class PatientTaskOverdueService(
    IPatientTaskRepository repository,
    IAuthenticatedClinicalUserAccessor clinicalUserAccessor) : IPatientTaskOverdueService
{
    public async Task<int> GetOverdueCountAsync(CancellationToken cancellationToken = default)
    {
        var userId = await clinicalUserAccessor.GetRequiredUserIdAsync(cancellationToken);
        return await repository.GetOverdueCountAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyList<OverduePatientTaskItem>> GetOverdueAsync(CancellationToken cancellationToken = default)
    {
        var userId = await clinicalUserAccessor.GetRequiredUserIdAsync(cancellationToken);
        return await repository.GetOverdueAsync(userId, cancellationToken);
    }
}
