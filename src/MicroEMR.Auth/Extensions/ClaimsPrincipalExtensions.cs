using System.Security.Claims;
using OpenIddict.Abstractions;

namespace MicroEMR.Auth.Extensions;


public static class ClaimsPrincipalExtensions
{
    public static void SetDestinations(
        this ClaimsPrincipal principal)
    {
        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken);
        }
    }
}