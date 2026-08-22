using Microsoft.AspNetCore.Authorization;
using MicroEMR.Application.PlatformEntitlements;
using MicroEMR.Application.Security;

namespace MicroEMR.Api.Authorization;

public sealed record PlatformEntitlementRequirement(string EntitlementKey) : IAuthorizationRequirement;

public sealed class PlatformEntitlementAuthorizationHandler
    : AuthorizationHandler<PlatformEntitlementRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformEntitlementRequirement requirement)
    {
        if (PlatformEntitlementKeys.IsKnown(requirement.EntitlementKey) &&
            context.User.Claims.Any(claim =>
                string.Equals(claim.Type, MicroEmrClaimTypes.PlatformEntitlement, StringComparison.Ordinal) &&
                string.Equals(claim.Value, requirement.EntitlementKey, StringComparison.Ordinal)))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePlatformEntitlementAttribute : AuthorizeAttribute
{
    public RequirePlatformEntitlementAttribute(string entitlementKey) =>
        Policy = PlatformEntitlementPolicies.For(entitlementKey);
}
