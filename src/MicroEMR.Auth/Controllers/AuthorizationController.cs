using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Auth.Data;
using MicroEMR.Auth.Extensions;
using MicroEMR.Auth.Services.Tenancy;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MicroEMR.Auth.Controllers;

public sealed class AuthorizationController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly ITenantClaimEnricher _tenantClaimEnricher;

    public AuthorizationController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictScopeManager scopeManager,
        ITenantClaimEnricher tenantClaimEnricher)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _scopeManager = scopeManager;
        _tenantClaimEnricher = tenantClaimEnricher;
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request =
            HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException(
                "The OpenID Connect request cannot be retrieved.");

        var authenticationResult =
            await HttpContext.AuthenticateAsync(
                IdentityConstants.ApplicationScheme);

        if (!authenticationResult.Succeeded ||
            authenticationResult.Principal is null)
        {
            var returnUrl =
                Request.PathBase +
                Request.Path +
                QueryString.Create(
                    Request.HasFormContentType
                        ? Request.Form
                            .Where(parameter =>
                                parameter.Key !=
                                Parameters.ClientSecret)
                            .ToList()
                        : Request.Query.ToList());

            return Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = returnUrl
                },
                IdentityConstants.ApplicationScheme);
        }

        var identityUser =
            await _userManager.GetUserAsync(
                authenticationResult.Principal);

        if (identityUser is null ||
            !identityUser.IsActive)
        {
            return Forbid(
                OpenIddictServerAspNetCoreDefaults
                    .AuthenticationScheme);
        }

        var identity = new ClaimsIdentity(
            authenticationType:
                OpenIddictServerAspNetCoreDefaults
                    .AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(
            Claims.Subject,
            await _userManager.GetUserIdAsync(identityUser));

        identity.SetClaim(
            Claims.Name,
            identityUser.FullName
            ?? identityUser.UserName
            ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(identityUser.Email))
        {
            identity.SetClaim(
                Claims.Email,
                identityUser.Email);
        }

        var roles =
            await _userManager.GetRolesAsync(identityUser);

        foreach (var role in roles)
        {
            identity.AddClaim(
                new Claim(Claims.Role, role));
        }

        var tenantResult = await _tenantClaimEnricher.EnrichAsync(
            identityUser,
            identity,
            HttpContext.TraceIdentifier,
            HttpContext.RequestAborted);

        if (tenantResult.Status != TenantClaimEnrichmentStatus.Resolved)
        {
            return Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] =
                        Errors.AccessDenied,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        tenantResult.ErrorDescription
                }),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var principal =
            new ClaimsPrincipal(identity);

        // Only grant scopes that were actually requested.
        principal.SetScopes(
            request.GetScopes());

        // Resolve resources attached to those scopes.
        var resources = new List<string>();

        await foreach (var resource in
            _scopeManager.ListResourcesAsync(
                principal.GetScopes()))
        {
            resources.Add(resource);
        }

        principal.SetResources(resources);

        principal.SetDestinations();

        return SignIn(
            principal,
            OpenIddictServerAspNetCoreDefaults
                .AuthenticationScheme);
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        return SignOut(
            OpenIddictServerAspNetCoreDefaults
                .AuthenticationScheme);
    }

}
