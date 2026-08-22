using System.Security.Claims;
using MicroEMR.Application.Security;
using OpenIddict.Abstractions;

namespace MicroEMR.Auth.Extensions;


public static class ClaimsPrincipalExtensions
{
    public static void SetDestinations(
        this ClaimsPrincipal principal)
    {
        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(GetDestinations(claim, principal));
        }
    }

    public static IEnumerable<string> GetDestinations(
        Claim claim,
        ClaimsPrincipal principal)
    {
        switch (claim.Type)
        {
            case OpenIddictConstants.Claims.Name:
                yield return OpenIddictConstants.Destinations.AccessToken;
                if (principal.HasScope(OpenIddictConstants.Scopes.Profile))
                    yield return OpenIddictConstants.Destinations.IdentityToken;
                yield break;
            case OpenIddictConstants.Claims.Email:
                yield return OpenIddictConstants.Destinations.AccessToken;
                if (principal.HasScope(OpenIddictConstants.Scopes.Email))
                    yield return OpenIddictConstants.Destinations.IdentityToken;
                yield break;
            case OpenIddictConstants.Claims.Role:
                yield return OpenIddictConstants.Destinations.AccessToken;
                if (principal.HasScope(OpenIddictConstants.Scopes.Roles))
                    yield return OpenIddictConstants.Destinations.IdentityToken;
                yield break;
            case OpenIddictConstants.Claims.Subject:
            case MicroEmrClaimTypes.TenantId:
            case MicroEmrClaimTypes.TenantKey:
                yield return OpenIddictConstants.Destinations.AccessToken;
                if (principal.HasScope(OpenIddictConstants.Scopes.OpenId))
                    yield return OpenIddictConstants.Destinations.IdentityToken;
                yield break;
            case MicroEmrClaimTypes.TenantName:
                yield return OpenIddictConstants.Destinations.AccessToken;
                if (principal.HasScope(OpenIddictConstants.Scopes.OpenId))
                    yield return OpenIddictConstants.Destinations.IdentityToken;
                yield break;
            case MicroEmrClaimTypes.TenantRole:
                yield return OpenIddictConstants.Destinations.AccessToken;
                if (principal.HasScope(OpenIddictConstants.Scopes.OpenId))
                    yield return OpenIddictConstants.Destinations.IdentityToken;
                yield break;
            case MicroEmrClaimTypes.PlatformEntitlement:
            case MicroEmrClaimTypes.PlatformAuthorizationVersion:
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield break;
            default:
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield break;
        }
    }
}
