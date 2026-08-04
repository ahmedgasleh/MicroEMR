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
    bool? ClinicalUserActive);

public interface ITenantUserAdministrationService
{
    Task<IReadOnlyList<TenantUserAdministrationItem>> GetTenantUsersAsync(
        CancellationToken cancellationToken = default);
}

public sealed class TenantUserAdministrationService(
    ITenantContext tenantContext,
    IPlatformMembershipAdministrationService memberships,
    IIdentityUserProfileLookup identityUsers,
    IClinicalUserRepository clinicalUsers,
    ILogger<TenantUserAdministrationService> logger) : ITenantUserAdministrationService
{
    public async Task<IReadOnlyList<TenantUserAdministrationItem>> GetTenantUsersAsync(
        CancellationToken cancellationToken = default)
    {
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
                clinical?.IsActive));
        }

        logger.LogInformation("Tenant user administration list loaded for tenant {TenantUid}; count {UserCount}.",
            tenantContext.TenantUid, results.Count);
        return results.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.UserName, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
