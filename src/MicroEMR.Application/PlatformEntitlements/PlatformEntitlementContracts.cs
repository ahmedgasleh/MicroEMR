using MicroEMR.Application.PlatformAdministration;

namespace MicroEMR.Application.PlatformEntitlements;

public static class PlatformEntitlementKeys
{
    public const string SecurityAuditView = "SecurityAudit.View";

    public static bool IsKnown(string key) =>
        string.Equals(key, SecurityAuditView, StringComparison.Ordinal);
}

public sealed record PlatformEntitlementChangeResult(
    Guid UserPlatformEntitlementUid,
    long AuthorizationVersion);

public interface IPlatformEntitlementRepository
{
    Task<IReadOnlyList<string>> GetActiveForUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<long> GetAuthorizationVersionAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<PlatformEntitlementChangeResult> AssignAsync(
        string userId,
        string entitlementKey,
        string actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<PlatformEntitlementChangeResult> RevokeAsync(
        string userId,
        string entitlementKey,
        string actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}

public interface IPlatformEntitlementService
{
    Task<IReadOnlyList<string>> GetActiveForUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<long> GetAuthorizationVersionAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<PlatformEntitlementChangeResult> AssignAsync(
        string userId,
        string entitlementKey,
        string actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    Task<PlatformEntitlementChangeResult> RevokeAsync(
        string userId,
        string entitlementKey,
        string actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class PlatformEntitlementService(
    IPlatformEntitlementRepository repository,
    IIdentityUserLookup identityUsers) : IPlatformEntitlementService
{
    public Task<IReadOnlyList<string>> GetActiveForUserAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        repository.GetActiveForUserAsync(RequiredUserId(userId), cancellationToken);

    public Task<long> GetAuthorizationVersionAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        repository.GetAuthorizationVersionAsync(RequiredUserId(userId), cancellationToken);

    public async Task<PlatformEntitlementChangeResult> AssignAsync(
        string userId,
        string entitlementKey,
        string actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = RequiredUserId(userId);
        ValidateChange(entitlementKey, actorUserId, correlationId);
        if (!identityUsers.IsAvailable)
            throw new InvalidOperationException("Auth user validation is not configured.");
        if (!await identityUsers.ExistsAsync(normalizedUserId, cancellationToken))
            throw new InvalidOperationException("The Auth subject does not identify an existing Auth user.");
        return await repository.AssignAsync(
            normalizedUserId, entitlementKey, actorUserId.Trim(), correlationId, cancellationToken);
    }

    public async Task<PlatformEntitlementChangeResult> RevokeAsync(
        string userId,
        string entitlementKey,
        string actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserId = RequiredUserId(userId);
        ValidateChange(entitlementKey, actorUserId, correlationId);
        if (!identityUsers.IsAvailable)
            throw new InvalidOperationException("Auth user validation is not configured.");
        if (!await identityUsers.ExistsAsync(normalizedUserId, cancellationToken))
            throw new InvalidOperationException("The Auth subject does not identify an existing Auth user.");
        return await repository.RevokeAsync(
            normalizedUserId, entitlementKey, actorUserId.Trim(), correlationId, cancellationToken);
    }

    private static string RequiredUserId(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var normalized = userId.Trim();
        if (normalized.Length > 450)
            throw new ArgumentException("The user identifier exceeds 450 characters.", nameof(userId));
        return normalized;
    }

    private static void ValidateChange(string entitlementKey, string actorUserId, Guid correlationId)
    {
        if (!PlatformEntitlementKeys.IsKnown(entitlementKey))
            throw new ArgumentException("The platform entitlement is not recognized.", nameof(entitlementKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUserId);
        if (actorUserId.Trim().Length > 450)
            throw new ArgumentException("The actor identifier exceeds 450 characters.", nameof(actorUserId));
        if (correlationId == Guid.Empty)
            throw new ArgumentException("A correlation identifier is required.", nameof(correlationId));
    }
}
