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
    async Task<TenantUserAdministrationItem?> GetTenantUserAsync(string authUserId,
        CancellationToken cancellationToken = default) =>
        (await GetTenantUsersAsync(cancellationToken)).SingleOrDefault(x =>
            string.Equals(x.AuthUserId, authUserId, StringComparison.Ordinal));
    Task<TenantUserAdministrationItem> DeactivateMembershipAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default);
    Task<TenantUserAdministrationItem> ActivateMembershipAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default);
    Task<TenantUserAdministrationItem> UpdateTenantRolesAsync(string authUserId,
        IReadOnlyCollection<string> selectedRoles, string rowVersion, CancellationToken cancellationToken = default);
    Task<TenantUserAdministrationItem> ProvisionClinicalUserAsync(string authUserId,
        CancellationToken cancellationToken = default);
}

public sealed class TenantUserAdministrationService(
    ITenantContext tenantContext,
    IPlatformMembershipAdministrationService memberships,
    IIdentityUserProfileLookup identityUsers,
    IClinicalUserRepository clinicalUsers,
    ITenantMembershipLifecycleRepository lifecycle,
    ITenantRoleManagementRepository roleManagement,
    IAuthenticatedSubjectAccessor subjectAccessor,
    ILogger<TenantUserAdministrationService> logger) : ITenantUserAdministrationService
{
    public async Task<IReadOnlyList<TenantUserAdministrationItem>> GetTenantUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var actorSubject = subjectAccessor.GetRequiredSubject();
        var tenantMemberships = await memberships.GetTenantMembershipsAsync(
            tenantContext.TenantUid, cancellationToken);
        var userIds = tenantMemberships.Select(x => x.UserId).Distinct(StringComparer.Ordinal).ToArray();
        var identities = await identityUsers.GetByIdsAsync(userIds, cancellationToken);
        var clinicalUsersBySubject = await clinicalUsers.GetByAuthSubjectIdsAsync(userIds, cancellationToken);
        var results = new List<TenantUserAdministrationItem>(tenantMemberships.Count);

        foreach (var membership in tenantMemberships)
        {
            var identity = identities.GetValueOrDefault(membership.UserId)
                ?? throw new InvalidDataException("A tenant membership references an unavailable Auth identity.");
            var clinical = clinicalUsersBySubject.GetValueOrDefault(identity.UserId);

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

    public async Task<TenantUserAdministrationItem?> GetTenantUserAsync(string authUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authUserId);
        return (await GetTenantUsersAsync(cancellationToken)).SingleOrDefault(x =>
            string.Equals(x.AuthUserId, authUserId, StringComparison.Ordinal));
    }

    public Task<TenantUserAdministrationItem> DeactivateMembershipAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default) =>
        ChangeMembershipAsync(authUserId, rowVersion, activate: false, cancellationToken);

    public Task<TenantUserAdministrationItem> ActivateMembershipAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default) =>
        ChangeMembershipAsync(authUserId, rowVersion, activate: true, cancellationToken);

    public async Task<TenantUserAdministrationItem> UpdateTenantRolesAsync(string authUserId,
        IReadOnlyCollection<string> selectedRoles, string rowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rowVersion);
        ArgumentNullException.ThrowIfNull(selectedRoles);
        string[] roles;
        try
        {
            roles = selectedRoles.Select(TenantRoleCatalog.Normalize).Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }
        catch (ArgumentException ex) { throw new TenantRoleValidationException("One or more tenant roles are not recognized.", ex); }
        if (roles.Length == 0) throw new TenantRoleValidationException("An active membership must have at least one tenant role.");
        var actor = subjectAccessor.GetRequiredSubject();
        await roleManagement.ReplaceRolesAsync(authUserId, tenantContext.TenantUid, roles, rowVersion, actor, cancellationToken);
        logger.LogInformation("Tenant roles updated for user {TargetUserId} in tenant {TenantUid} by {ActorUserId}.",
            authUserId, tenantContext.TenantUid, actor);
        return (await GetTenantUsersAsync(cancellationToken)).Single(x =>
            string.Equals(x.AuthUserId, authUserId, StringComparison.Ordinal));
    }

    public async Task<TenantUserAdministrationItem> ProvisionClinicalUserAsync(string authUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authUserId);
        var membership = (await memberships.GetTenantMembershipsAsync(tenantContext.TenantUid, cancellationToken))
            .SingleOrDefault(x => string.Equals(x.UserId, authUserId, StringComparison.Ordinal));
        if (membership is null)
            throw new TenantMembershipNotFoundException("The membership was not found in the active tenant.");
        if (!string.Equals(membership.MembershipStatus, "Active", StringComparison.Ordinal))
            throw new TenantClinicalProvisioningNotEligibleException(
                "Activate the membership before provisioning a clinical user.");

        var identity = await identityUsers.GetByIdAsync(membership.UserId, cancellationToken)
            ?? throw new TenantClinicalProvisioningIdentityNotFoundException(
                "The Auth identity for this tenant member is unavailable.");
        if (!identity.IsActive || !string.Equals(identity.UserId, membership.UserId, StringComparison.Ordinal))
            throw new TenantClinicalProvisioningNotEligibleException(
                "The Auth account must be active before provisioning a clinical user.");

        var clinical = await clinicalUsers.ProvisionAsync(identity.UserId, identity.Username,
            identity.DisplayName, identity.Email, cancellationToken);
        var actor = subjectAccessor.GetRequiredSubject();
        logger.LogInformation(
            "Clinical user {ClinicalUserId} provisioned or resolved for Auth user {TargetUserId} in tenant {TenantUid} by administrator {ActorUserId}.",
            clinical.UserId, identity.UserId, tenantContext.TenantUid, actor);
        return (await GetTenantUsersAsync(cancellationToken)).Single(x =>
            string.Equals(x.AuthUserId, identity.UserId, StringComparison.Ordinal));
    }

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
