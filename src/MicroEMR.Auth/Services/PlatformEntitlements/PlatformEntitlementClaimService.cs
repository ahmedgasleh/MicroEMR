using MicroEMR.Application.PlatformEntitlements;

namespace MicroEMR.Auth.Services.PlatformEntitlements;

public sealed record PlatformAuthorizationSnapshot(
    IReadOnlyList<string> Entitlements,
    long AuthorizationVersion);

public interface IPlatformEntitlementClaimService
{
    Task<PlatformAuthorizationSnapshot> LoadAsync(
        string identityUserId,
        CancellationToken cancellationToken = default);
}

public interface IPlatformRefreshAuthorizationService
{
    Task<PlatformAuthorizationSnapshot?> ValidateAndLoadAsync(
        string identityUserId,
        long trustedAuthorizationVersion,
        CancellationToken cancellationToken = default);
}

public sealed class PlatformRefreshAuthorizationService(
    IPlatformEntitlementClaimService claims) : IPlatformRefreshAuthorizationService
{
    public async Task<PlatformAuthorizationSnapshot?> ValidateAndLoadAsync(
        string identityUserId,
        long trustedAuthorizationVersion,
        CancellationToken cancellationToken = default)
    {
        var loaded = await claims.LoadAsync(identityUserId, cancellationToken);
        if (loaded.AuthorizationVersion != trustedAuthorizationVersion)
            return null;

        var confirmed = await claims.LoadAsync(identityUserId, cancellationToken);
        return confirmed.AuthorizationVersion == trustedAuthorizationVersion ? confirmed : null;
    }
}

public sealed class PlatformEntitlementClaimService(
    IPlatformEntitlementService entitlements) : IPlatformEntitlementClaimService
{
    private const int MaximumEntitlementsPerUser = 32;

    public async Task<PlatformAuthorizationSnapshot> LoadAsync(
        string identityUserId,
        CancellationToken cancellationToken = default)
    {
        var active = await entitlements.GetActiveForUserAsync(identityUserId, cancellationToken);
        if (active.Count > MaximumEntitlementsPerUser)
        {
            throw new InvalidOperationException(
                "The platform entitlement result exceeds the governed token limit.");
        }

        var governed = active
            .Where(PlatformEntitlementKeys.IsKnown)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var version = await entitlements.GetAuthorizationVersionAsync(
            identityUserId,
            cancellationToken);

        return new PlatformAuthorizationSnapshot(governed, version);
    }
}
