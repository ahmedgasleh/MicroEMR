namespace MicroEMR.Application.ClinicalUsers;

public interface IClinicalUserRepository
{
    Task<ClinicalUser?> GetByAuthSubjectIdAsync(
        string authSubjectId,
        CancellationToken cancellationToken = default);

    async Task<IReadOnlyDictionary<string, ClinicalUser>> GetByAuthSubjectIdsAsync(
        IReadOnlyCollection<string> authSubjectIds,
        CancellationToken cancellationToken = default)
    {
        var users = new Dictionary<string, ClinicalUser>(StringComparer.Ordinal);
        foreach (var subject in authSubjectIds.Distinct(StringComparer.Ordinal))
        {
            var user = await GetByAuthSubjectIdAsync(subject, cancellationToken);
            if (user is not null) users[user.AuthSubjectId] = user;
        }
        return users;
    }

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

public class ClinicalUserProvisioningException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class ClinicalUserProvisioningConflictException(string message, Exception? innerException = null)
    : ClinicalUserProvisioningException(message, innerException);
