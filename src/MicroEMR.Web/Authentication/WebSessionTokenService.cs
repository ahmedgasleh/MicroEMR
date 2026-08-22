using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace MicroEMR.Web.Authentication;

public interface IWebSessionTokenService
{
    Task<string> GetValidAccessTokenAsync(HttpContext context, CancellationToken cancellationToken);
    Task RefreshCookieTicketAsync(CookieValidatePrincipalContext context);
}

public sealed class WebSessionTokenService(
    IRefreshTokenClient refreshTokenClient,
    ISessionTokenRefreshCoordinator coordinator,
    IOptions<WebTokenRefreshOptions> options,
    TimeProvider timeProvider,
    ILogger<WebSessionTokenService> logger) : IWebSessionTokenService
{
    public async Task<string> GetValidAccessTokenAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var authentication = await context.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal is null || authentication.Properties is null)
        {
            throw new UnauthorizedAccessException("The authenticated Web session is unavailable.");
        }

        try
        {
            var result = await EnsureValidAsync(
                context,
                authentication.Properties,
                allowTicketReauthentication: true,
                cancellationToken);
            if (result.Refreshed)
            {
                await context.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    authentication.Principal,
                    authentication.Properties);
                logger.LogDebug("The authenticated Web session token was renewed.");
            }

            return result.AccessToken;
        }
        catch (TokenRefreshInvalidGrantException)
        {
            await InvalidateSessionAsync(context);
            throw new WebSessionReauthenticationRequiredException();
        }
    }

    public async Task RefreshCookieTicketAsync(CookieValidatePrincipalContext context)
    {
        if (context.Principal is null)
        {
            context.RejectPrincipal();
            return;
        }

        try
        {
            var result = await EnsureValidAsync(
                context.HttpContext,
                context.Properties,
                allowTicketReauthentication: false,
                context.HttpContext.RequestAborted);
            if (result.Refreshed)
            {
                context.ShouldRenew = true;
                logger.LogDebug("The authenticated Web session token was renewed during cookie validation.");
            }
        }
        catch (TokenRefreshInvalidGrantException)
        {
            context.RejectPrincipal();
            await InvalidateSessionAsync(context.HttpContext);
        }
    }

    private async Task<AccessTokenResult> EnsureValidAsync(
        HttpContext context,
        AuthenticationProperties properties,
        bool allowTicketReauthentication,
        CancellationToken cancellationToken)
    {
        var accessToken = properties.GetTokenValue("access_token");
        var expiresAt = ReadExpiry(properties.GetTokenValue("expires_at"));
        if (!string.IsNullOrWhiteSpace(accessToken) &&
            expiresAt > timeProvider.GetUtcNow() + options.Value.RefreshThreshold)
            return new AccessTokenResult(accessToken, false);

        var refreshToken = properties.GetTokenValue("refresh_token");
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new TokenRefreshInvalidGrantException();

        RefreshedTokenSet refreshed;
        refreshed = await coordinator.RunOnceAsync(
                refreshToken,
                async token =>
                {
                    // Re-evaluate after serialization: a preceding request may have renewed
                    // authentication state before this request entered the critical section.
                    if (allowTicketReauthentication)
                    {
                        var latest = await context.AuthenticateAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme);
                        if (latest.Succeeded && latest.Properties is not null)
                        {
                            var latestAccessToken = latest.Properties.GetTokenValue("access_token");
                            var latestRefreshToken = latest.Properties.GetTokenValue("refresh_token");
                            var latestExpiry = ReadExpiry(latest.Properties.GetTokenValue("expires_at"));
                            if (!string.IsNullOrWhiteSpace(latestAccessToken) &&
                                !string.IsNullOrWhiteSpace(latestRefreshToken) &&
                                !string.Equals(latestRefreshToken, refreshToken, StringComparison.Ordinal) &&
                                latestExpiry > timeProvider.GetUtcNow() + options.Value.RefreshThreshold)
                            {
                                return new RefreshedTokenSet(
                                    latestAccessToken,
                                    latestRefreshToken,
                                    latestExpiry);
                            }
                        }
                    }

                    return await refreshTokenClient.RedeemAsync(refreshToken, token);
                },
                cancellationToken);
        properties.UpdateTokenValue("access_token", refreshed.AccessToken);
        properties.UpdateTokenValue("refresh_token", refreshed.RefreshToken);
        properties.UpdateTokenValue(
            "expires_at",
            refreshed.ExpiresAt.ToString("O", CultureInfo.InvariantCulture));
        return new AccessTokenResult(refreshed.AccessToken, true);
    }

    private static DateTimeOffset ReadExpiry(string? value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var expiresAt)
            ? expiresAt
            : DateTimeOffset.MinValue;

    private static Task InvalidateSessionAsync(HttpContext context) =>
        context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    private sealed record AccessTokenResult(string AccessToken, bool Refreshed);
}
