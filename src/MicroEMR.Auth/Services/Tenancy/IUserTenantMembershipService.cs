using MicroEMR.Application.Tenancy;
using MicroEMR.Auth.Data;

namespace MicroEMR.Auth.Services.Tenancy;

public interface IUserTenantMembershipService
{
    Task<IReadOnlyList<UserTenantMembershipInfo>> GetActiveMembershipsAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default);

    Task<UserTenantMembershipInfo?> GetDefaultMembershipAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default);
}
