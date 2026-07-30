using Microsoft.AspNetCore.Authorization;
using MicroEMR.Application.Security;

namespace MicroEMR.Api.Authorization;

public sealed class TenantRoleAuthorizationHandler
    : AuthorizationHandler<TenantRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        if (context.User.FindAll(MicroEmrClaimTypes.TenantRole)
            .Any(claim => string.Equals(
                claim.Value,
                requirement.Role,
                StringComparison.Ordinal)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
