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
}
