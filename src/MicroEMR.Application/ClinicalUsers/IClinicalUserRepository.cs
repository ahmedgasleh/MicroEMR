namespace MicroEMR.Application.ClinicalUsers;

public interface IClinicalUserRepository
{
    Task<ClinicalUser?> GetByAuthSubjectIdAsync(
        string authSubjectId,
        CancellationToken cancellationToken = default);

    Task<ClinicalUser> SetAuthSubjectIdAsync(
        long userId,
        string authSubjectId,
        CancellationToken cancellationToken = default);

    Task<ClinicalUser> ProvisionAsync(
        string authSubjectId,
        string username,
        string displayName,
        string? email,
        CancellationToken cancellationToken = default);
}
