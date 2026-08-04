using Microsoft.Extensions.Logging;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PlatformAdministration;
using MicroEMR.Application.Tenancy;

namespace MicroEMR.Application.TenantUserAdministration;

public sealed record TenantUserAdministrationItem(
    string AuthUserId,
    string UserName,
    string DisplayName,
    string? Email,
    bool AuthUserActive,
    string MembershipStatus,
    IReadOnlyCollection<string> TenantRoles,
    bool ClinicalUserProvisioned,
    long? ClinicalUserId,
    bool? ClinicalUserActive,
    DateTimeOffset? MembershipUpdatedAt,
    string RowVersion,
    bool IsCurrentUser);

public interface ITenantUserAdministrationService
{
    Task<IReadOnlyList<TenantUserAdministrationItem>> GetTenantUsersAsync(
        CancellationToken cancellationToken = default);
    Task<TenantUserAdministrationItem> DeactivateMembershipAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default);
    Task<TenantUserAdministrationItem> ActivateMembershipAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default);
}

public sealed class TenantUserAdministrationService(
    ITenantContext tenantContext,
    IPlatformMembershipAdministrationService memberships,
    IIdentityUserProfileLookup identityUsers,
    IClinicalUserRepository clinicalUsers,
    ITenantMembershipLifecycleRepository lifecycle,
    IAuthenticatedSubjectAccessor subjectAccessor,
    ILogger<TenantUserAdministrationService> logger) : ITenantUserAdministrationService
{
    public async Task<IReadOnlyList<TenantUserAdministrationItem>> GetTenantUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var actorSubject = subjectAccessor.GetRequiredSubject();
        var tenantMemberships = await memberships.GetTenantMembershipsAsync(
            tenantContext.TenantUid, cancellationToken);
        var results = new List<TenantUserAdministrationItem>(tenantMemberships.Count);

        foreach (var membership in tenantMemberships)
        {
            var identity = await identityUsers.GetByIdAsync(membership.UserId, cancellationToken)
                ?? throw new InvalidDataException("A tenant membership references an unavailable Auth identity.");
            var clinical = await clinicalUsers.GetByAuthSubjectIdAsync(identity.UserId, cancellationToken);

            results.Add(new TenantUserAdministrationItem(
                identity.UserId,
                identity.Username,
                identity.DisplayName,
                identity.Email,
                identity.IsActive,
                membership.MembershipStatus,
                membership.Roles.ToArray(),
                clinical is not null,
                clinical?.UserId,
                clinical?.IsActive,
                membership.UpdatedAt,
                membership.RowVersion ?? throw new InvalidDataException("Membership concurrency metadata is unavailable."),
                string.Equals(identity.UserId, actorSubject, StringComparison.Ordinal)));
        }

        logger.LogInformation("Tenant user administration list loaded for tenant {TenantUid}; count {UserCount}.",
            tenantContext.TenantUid, results.Count);
        return results.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.UserName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public Task<TenantUserAdministrationItem> DeactivateMembershipAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default) =>
        ChangeMembershipAsync(authUserId, rowVersion, activate: false, cancellationToken);

    public Task<TenantUserAdministrationItem> ActivateMembershipAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default) =>
        ChangeMembershipAsync(authUserId, rowVersion, activate: true, cancellationToken);

    private async Task<TenantUserAdministrationItem> ChangeMembershipAsync(string authUserId, string rowVersion,
        bool activate, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rowVersion);
        var actor = subjectAccessor.GetRequiredSubject();
        if (activate)
            await lifecycle.ActivateAsync(authUserId, tenantContext.TenantUid, rowVersion, actor, cancellationToken);
        else
            await lifecycle.DeactivateAsync(authUserId, tenantContext.TenantUid, rowVersion, actor, cancellationToken);
        return (await GetTenantUsersAsync(cancellationToken)).Single(x =>
            string.Equals(x.AuthUserId, authUserId, StringComparison.Ordinal));
    }
}
