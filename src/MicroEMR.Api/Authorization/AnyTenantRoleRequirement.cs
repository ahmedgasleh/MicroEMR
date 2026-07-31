using Microsoft.AspNetCore.Authorization;
using MicroEMR.Application.Security;

namespace MicroEMR.Api.Authorization;

public sealed record AnyTenantRoleRequirement(params string[] Roles) : IAuthorizationRequirement;

public sealed class AnyTenantRoleAuthorizationHandler : AuthorizationHandler<AnyTenantRoleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AnyTenantRoleRequirement requirement)
    {
        var assignedRoles = context.User.FindAll(MicroEmrClaimTypes.TenantRole).Select(claim => claim.Value);
        if (assignedRoles.Any(role => requirement.Roles.Contains(role, StringComparer.OrdinalIgnoreCase)))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
