using Microsoft.AspNetCore.Authorization;
using MicroEMR.Application.PlatformEntitlements;
using MicroEMR.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.JsonWebTokens;

namespace MicroEMR.Web.Authorization;

public sealed record PlatformEntitlementRequirement(string EntitlementKey) : IAuthorizationRequirement;

public interface IWebPlatformEntitlementAccessor
{
    Task<bool> HasAsync(string entitlementKey, CancellationToken cancellationToken = default);
}

public sealed class WebPlatformEntitlementAccessor(IHttpContextAccessor contextAccessor)
    : IWebPlatformEntitlementAccessor
{
    public async Task<bool> HasAsync(
        string entitlementKey,
        CancellationToken cancellationToken = default)
    {
        if (!PlatformEntitlementKeys.IsKnown(entitlementKey))
            return false;

        var context = contextAccessor.HttpContext;
        if (context is null)
            return false;

        var authentication = await context.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
        var token = authentication.Properties?.GetTokenValue("access_token");
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            // The token came from Auth and is integrity-protected inside the Web cookie ticket.
            var accessToken = new JsonWebToken(token);
            return accessToken.Claims.Any(claim =>
                string.Equals(claim.Type, MicroEmrClaimTypes.PlatformEntitlement, StringComparison.Ordinal) &&
                string.Equals(claim.Value, entitlementKey, StringComparison.Ordinal));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

public sealed class PlatformEntitlementAuthorizationHandler(
    IWebPlatformEntitlementAccessor entitlements)
    : AuthorizationHandler<PlatformEntitlementRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformEntitlementRequirement requirement)
    {
        if (PlatformEntitlementKeys.IsKnown(requirement.EntitlementKey) &&
            (context.User.Claims.Any(claim =>
                string.Equals(claim.Type, MicroEmrClaimTypes.PlatformEntitlement, StringComparison.Ordinal) &&
                string.Equals(claim.Value, requirement.EntitlementKey, StringComparison.Ordinal)) ||
             await entitlements.HasAsync(requirement.EntitlementKey)))
            context.Succeed(requirement);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePlatformEntitlementAttribute : AuthorizeAttribute
{
    public RequirePlatformEntitlementAttribute(string entitlementKey) =>
        Policy = PlatformEntitlementPolicies.For(entitlementKey);
}
