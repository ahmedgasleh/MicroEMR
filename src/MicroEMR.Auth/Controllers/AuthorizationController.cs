using System.Security.Claims;
using MicroEMR.Auth.Data;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using MicroEMR.Auth.Extensions;

namespace MicroEMR.Auth.Controllers;


public class AuthorizationController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;


    public AuthorizationController(
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }


    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request =
            HttpContext.GetOpenIddictServerRequest()
            ?? throw new Exception("OIDC request missing");


        // not logged in yet
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Challenge(
                new AuthenticationProperties
                {
                    RedirectUri =
                    Request.PathBase +
                    Request.Path +
                    QueryString.Create(
                        Request.Query
                        .Select(x =>
                        new KeyValuePair<string, string?>(
                            x.Key,
                            x.Value)))
                },
                IdentityConstants.ApplicationScheme);
        }


        var user =
            await _userManager.GetUserAsync(User);


        if (user == null)
        {
            return Forbid();
        }


        var identity =
            new ClaimsIdentity(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);


        identity.AddClaim(
            OpenIddictConstants.Claims.Subject,
            user.Id);


        identity.AddClaim(
            OpenIddictConstants.Claims.Email,
            user.Email ?? "");


        identity.AddClaim(
            OpenIddictConstants.Claims.Name,
            user.FullName ?? user.UserName ?? "");


        var roles =
            await _userManager.GetRolesAsync(user);


        foreach (var role in roles)
        {
            identity.AddClaim(
                OpenIddictConstants.Claims.Role,
                role);
        }


        var principal =
            new ClaimsPrincipal(identity);


        principal.SetScopes(request.GetScopes());


        principal.SetResources(
            "microemr_api");

        principal.SetDestinations();

        return SignIn(
            principal,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        var request = HttpContext.GetOpenIddictServerRequest();

        var redirectUri = request?.PostLogoutRedirectUri;

        if (!string.IsNullOrWhiteSpace(redirectUri))
        {
            return Redirect(redirectUri);
        }

        return Redirect("/");
    }
}