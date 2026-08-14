using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Services.TenantUserAdministration;

namespace MicroEMR.Web.Authorization;

public interface IWebPermissionService
{
    Task<IReadOnlySet<string>> GetAsync(CancellationToken token = default);
    Task<bool> HasAsync(string key, CancellationToken token = default);
}

public sealed class WebPermissionService(IAccessProfileApiClient client) : IWebPermissionService
{
    private Task<IReadOnlySet<string>>? _permissions;
    public Task<IReadOnlySet<string>> GetAsync(CancellationToken token = default) =>
        _permissions ??= client.EffectivePermissionsAsync(token);
    public async Task<bool> HasAsync(string key, CancellationToken token = default) =>
        PermissionCatalog.IsKnown(key) && (await GetAsync(token)).Contains(key);
}

public sealed record WebPermissionRequirement(string Key) : IAuthorizationRequirement;

public sealed class WebPermissionHandler(IWebPermissionService permissions)
    : AuthorizationHandler<WebPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, WebPermissionRequirement requirement)
    {
        if (await permissions.HasAsync(requirement.Key)) context.Succeed(requirement);
    }

}

public sealed class WebPermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "Permission:";
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string name)
    {
        if (!name.StartsWith(Prefix, StringComparison.Ordinal)) return base.GetPolicyAsync(name);
        var key = name[Prefix.Length..];
        return Task.FromResult<AuthorizationPolicy?>(PermissionCatalog.IsKnown(key)
            ? new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new WebPermissionRequirement(key)).Build()
            : null);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireWebPermissionAttribute(string key) : AuthorizeAttribute(WebPermissionPolicyProvider.Prefix + key);
