using MicroEMR.Application.Tenancy;
using MicroEMR.Auth.Data;

namespace MicroEMR.Auth.Services.Tenancy;

public sealed class UserTenantMembershipService
    : IUserTenantMembershipService
{
    private readonly IUserTenantMembershipRepository _repository;

    public UserTenantMembershipService(
        IUserTenantMembershipRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<UserTenantMembershipInfo>>
        GetActiveMembershipsAsync(
            ApplicationUser user,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.Id);

        return _repository.GetActiveMembershipsAsync(user.Id, cancellationToken);
    }

    public async Task<UserTenantMembershipInfo?> GetDefaultMembershipAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var memberships =
            await GetActiveMembershipsAsync(user, cancellationToken);

        if (memberships.Count == 0)
        {
            return null;
        }

        if (memberships.Count == 1)
        {
            return memberships[0];
        }

        var defaults = memberships
            .Where(membership => membership.IsDefaultTenant)
            .ToArray();

        return defaults.Length switch
        {
            0 => null,
            1 => defaults[0],
            _ => throw new InvalidTenantMembershipDataException(
                user.Id,
                "Multiple active tenant memberships are marked as default.")
        };
    }
}
