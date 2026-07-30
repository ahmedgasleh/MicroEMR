using MicroEMR.Application.Tenancy;
using MicroEMR.Auth.Data;

namespace MicroEMR.Auth.Services.Tenancy;

public sealed class UserTenantResolver : IUserTenantResolver
{
    private readonly IUserTenantMembershipService _membershipService;

    public UserTenantResolver(
        IUserTenantMembershipService membershipService)
    {
        _membershipService = membershipService;
    }

    public async Task<TenantMembershipResolutionResult> ResolveAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var memberships =
            await _membershipService.GetActiveMembershipsAsync(
                user,
                cancellationToken);

        if (memberships.Count == 0)
        {
            return new TenantMembershipResolutionResult(
                TenantMembershipResolutionStatus.None,
                Membership: null,
                Array.Empty<UserTenantMembershipInfo>());
        }

        if (memberships.Count == 1)
        {
            return new TenantMembershipResolutionResult(
                TenantMembershipResolutionStatus.Resolved,
                memberships[0],
                memberships);
        }

        var defaults = memberships
            .Where(membership => membership.IsDefaultTenant)
            .ToArray();

        if (defaults.Length > 1)
        {
            throw new InvalidTenantMembershipDataException(
                user.Id,
                "Multiple active tenant memberships are marked as default.");
        }

        return defaults.Length == 1
            ? new TenantMembershipResolutionResult(
                TenantMembershipResolutionStatus.Resolved,
                defaults[0],
                memberships)
            : new TenantMembershipResolutionResult(
                TenantMembershipResolutionStatus.SelectionRequired,
                Membership: null,
                memberships);
    }
}
