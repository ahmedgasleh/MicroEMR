using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Auth.Data;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MicroEMR.Auth.Controllers;

public sealed class AuthorizationController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IOpenIddictScopeManager _scopeManager;

    public AuthorizationController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictScopeManager scopeManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _scopeManager = scopeManager;
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

        SetClaimDestinations(principal);

        return SignIn(
            principal,
            OpenIddictServerAspNetCoreDefaults
                .AuthenticationScheme);
    }

    private static void SetClaimDestinations(
        ClaimsPrincipal principal)
    {
        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(
                GetDestinations(claim, principal));
        }
    }

    private static IEnumerable<string> GetDestinations(
        Claim claim,
        ClaimsPrincipal principal)
    {
        switch (claim.Type)
        {
            case Claims.Name:
                yield return Destinations.AccessToken;

                if (principal.HasScope(Scopes.Profile))
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Email:
                yield return Destinations.AccessToken;

                if (principal.HasScope(Scopes.Email))
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;

                if (principal.HasScope(Scopes.Roles))
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Subject:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }
}