using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace MicroEMR.Web.Authentication;

public interface IRefreshTokenClient
{
    Task<RefreshedTokenSet> RedeemAsync(string refreshToken, CancellationToken cancellationToken);
}

public sealed class OpenIdConnectRefreshTokenClient(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<OpenIdConnectOptions> optionsMonitor,
    TimeProvider timeProvider,
    ILogger<OpenIdConnectRefreshTokenClient> logger) : IRefreshTokenClient
{
    public const string HttpClientName = "MicroEMR.Auth.TokenRefresh";

    public async Task<RefreshedTokenSet> RedeemAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var options = optionsMonitor.Get(OpenIdConnectDefaults.AuthenticationScheme);
        var configuration = options.Configuration
            ?? await (options.ConfigurationManager
                ?? throw new InvalidOperationException("OpenID Connect configuration is unavailable."))
                .GetConfigurationAsync(cancellationToken);

        if (!Uri.TryCreate(configuration.TokenEndpoint, UriKind.Absolute, out var tokenEndpoint))
        {
            throw new InvalidOperationException("The trusted OpenID Connect metadata has no valid token endpoint.");
        }

        var clientId = options.ClientId
            ?? throw new InvalidOperationException("The OpenID Connect client ID is unavailable.");
        var clientSecret = options.ClientSecret
            ?? throw new InvalidOperationException("The OpenID Connect client secret is unavailable.");

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            })
        };

        HttpResponseMessage response;
        try
        {
            response = await httpClientFactory.CreateClient(HttpClientName)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("The OpenID Connect token refresh request timed out.");
            throw new TokenRefreshTemporarilyUnavailableException();
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "The OpenID Connect token refresh request failed.");
            throw new TokenRefreshTemporarilyUnavailableException(exception);
        }

        using (response)
        {
            TokenEndpointResponse? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<TokenEndpointResponse>(
                    cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                logger.LogWarning("The OpenID Connect token endpoint returned an unreadable response.");
                throw new TokenRefreshTemporarilyUnavailableException();
            }

            if (!response.IsSuccessStatusCode)
            {
                if (string.Equals(payload?.Error, "invalid_grant", StringComparison.Ordinal))
                {
                    logger.LogInformation("The Web session refresh grant is no longer valid.");
                    throw new TokenRefreshInvalidGrantException();
                }

                logger.LogWarning(
                    "The OpenID Connect token endpoint returned status code {StatusCode} during refresh.",
                    (int)response.StatusCode);
                throw new TokenRefreshTemporarilyUnavailableException();
            }

            if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken) || payload.ExpiresIn <= 0)
            {
                logger.LogWarning("The OpenID Connect token endpoint returned an incomplete refresh response.");
                throw new TokenRefreshTemporarilyUnavailableException();
            }

            return new RefreshedTokenSet(
                payload.AccessToken,
                string.IsNullOrWhiteSpace(payload.RefreshToken) ? refreshToken : payload.RefreshToken,
                timeProvider.GetUtcNow().AddSeconds(payload.ExpiresIn));
        }
    }

    private sealed class TokenEndpointResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }
}
