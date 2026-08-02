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
}
