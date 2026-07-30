using MicroEMR.Auth.Data;

namespace MicroEMR.Auth.Services.Tenancy;

public interface IUserTenantResolver
{
    Task<TenantMembershipResolutionResult> ResolveAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default);
}
