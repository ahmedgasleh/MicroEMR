namespace MicroEMR.Application.PlatformAdministration;

public sealed record IdentityUserProfile(
    string UserId,
    string Username,
    string DisplayName,
    string? Email,
    bool IsActive);

public interface IIdentityUserProfileLookup
{
    Task<IdentityUserProfile?> GetByIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    async Task<IReadOnlyDictionary<string, IdentityUserProfile>> GetByIdsAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var profiles = new Dictionary<string, IdentityUserProfile>(StringComparer.Ordinal);
        foreach (var userId in userIds.Distinct(StringComparer.Ordinal))
        {
            var profile = await GetByIdAsync(userId, cancellationToken);
            if (profile is not null) profiles[profile.UserId] = profile;
        }
        return profiles;
    }
}
