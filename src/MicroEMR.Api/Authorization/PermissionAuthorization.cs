using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Api.Authorization;

public sealed record PermissionRequirement(string PermissionKey) : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler(
    ICurrentUserPermissionService permissions,
    ILogger<PermissionAuthorizationHandler> logger) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (await permissions.HasPermissionAsync(requirement.PermissionKey))
        {
            context.Succeed(requirement);
            return;
        }

        logger.LogWarning("Permission denied for subject {Subject}; tenant permission {PermissionKey}.",
            context.User.FindFirst("sub")?.Value ?? "unknown", requirement.PermissionKey);
    }

}

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "Permission:";

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
            return base.GetPolicyAsync(policyName);

        var permissionKey = policyName[Prefix.Length..];
        if (!PermissionCatalog.IsKnown(permissionKey))
            return Task.FromResult<AuthorizationPolicy?>(null);

        return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissionKey))
            .Build());
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permissionKey)
    {
        if (!PermissionCatalog.IsKnown(permissionKey))
            throw new ArgumentException("Unknown permission key.", nameof(permissionKey));
        Policy = PermissionPolicyProvider.Prefix + permissionKey;
    }
}
