using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MicroEMR.Web.Authentication;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class WebTokenRefreshTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidTokenIsReturnedWithoutRefreshOrCookieRewrite()
    {
        var authentication = new FakeAuthenticationService(CreateTicket(Now.AddMinutes(10)));
        var refresh = new FakeRefreshTokenClient();
        var context = CreateContext(authentication);
        var service = CreateService(refresh);

        var token = await service.GetValidAccessTokenAsync(context, default);

        Assert.Equal("access-old", token);
        Assert.Equal(0, refresh.CallCount);
        Assert.Equal(0, authentication.SignInCount);
    }

    [Fact]
    public async Task NearExpiryRefreshRotatesTokensUpdatesExpiryAndRenewsCookie()
    {
        var authentication = new FakeAuthenticationService(CreateTicket(Now.AddSeconds(30)));
        var refresh = new FakeRefreshTokenClient
        {
            Result = new("access-new", "refresh-new", Now.AddHours(1))
        };
        var context = CreateContext(authentication);

        var token = await CreateService(refresh).GetValidAccessTokenAsync(context, default);

        Assert.Equal("access-new", token);
        Assert.Equal(1, refresh.CallCount);
        Assert.Equal(1, authentication.SignInCount);
        var signedInProperties = authentication.SignedInProperties!;
        Assert.Equal("access-new", signedInProperties.GetTokenValue("access_token"));
        Assert.Equal("refresh-new", signedInProperties.GetTokenValue("refresh_token"));
        Assert.Equal(Now.AddHours(1), signedInProperties.ExpiresUtc);
        Assert.Equal(
            Now.AddHours(1),
            DateTimeOffset.Parse(
                signedInProperties.GetTokenValue("expires_at")!,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind));
    }

    [Fact]
    public async Task ConcurrentRefreshCallsRedeemCurrentRefreshTokenOnce()
    {
        var coordinator = new SessionTokenRefreshCoordinator(new FixedTimeProvider(Now));
        var calls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<RefreshedTokenSet> Refresh(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            await release.Task.WaitAsync(cancellationToken);
            return new("access-new", "refresh-new", Now.AddHours(1));
        }

        var requests = Enumerable.Range(0, 12)
            .Select(_ => coordinator.RunOnceAsync("refresh-old", Refresh, default))
            .ToArray();
        release.SetResult();
        var results = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, calls);
        Assert.All(results, result => Assert.Equal("access-new", result.AccessToken));
        Assert.All(results, result => Assert.Equal("refresh-new", result.RefreshToken));
    }

    [Fact]
    public async Task ConcurrentInvalidGrantIsNotRedeemedRepeatedly()
    {
        var coordinator = new SessionTokenRefreshCoordinator(new FixedTimeProvider(Now));
        var calls = 0;

        Task<RefreshedTokenSet> Refresh(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            throw new TokenRefreshInvalidGrantException();
        }

        var requests = Enumerable.Range(0, 8)
            .Select(async _ => await Assert.ThrowsAsync<TokenRefreshInvalidGrantException>(
                () => coordinator.RunOnceAsync("invalid-refresh", Refresh, default)));

        await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task InvalidGrantClearsCookieAndPreventsAuthenticatedApiCall()
    {
        var authentication = new FakeAuthenticationService(CreateTicket(Now.AddSeconds(30)));
        var refresh = new FakeRefreshTokenClient { Exception = new TokenRefreshInvalidGrantException() };
        var context = CreateContext(authentication);
        var api = new RecordingHandler();
        var handler = new WebApiBearerTokenHandler(
            new HttpContextAccessor { HttpContext = context },
            CreateService(refresh))
        {
            InnerHandler = api
        };

        await Assert.ThrowsAsync<WebSessionReauthenticationRequiredException>(
            () => new HttpClient(handler).GetAsync("https://api.test/patients"));

        Assert.Equal(1, authentication.SignOutCount);
        Assert.Equal(0, api.CallCount);
        Assert.Equal(1, refresh.CallCount);
    }

    [Fact]
    public async Task TemporaryFailurePreservesSessionAndTokens()
    {
        var ticket = CreateTicket(Now.AddSeconds(30));
        var authentication = new FakeAuthenticationService(ticket);
        var refresh = new FakeRefreshTokenClient
        {
            Exception = new TokenRefreshTemporarilyUnavailableException()
        };

        await Assert.ThrowsAsync<TokenRefreshTemporarilyUnavailableException>(
            () => CreateService(refresh).GetValidAccessTokenAsync(CreateContext(authentication), default));

        Assert.Equal(0, authentication.SignOutCount);
        Assert.Equal(0, authentication.SignInCount);
        Assert.Equal("access-old", ticket.Properties.GetTokenValue("access_token"));
        Assert.Equal("refresh-old", ticket.Properties.GetTokenValue("refresh_token"));
    }

    [Fact]
    public async Task ApiHandlerReplacesLegacyBearerHeaderWithRefreshedToken()
    {
        var authentication = new FakeAuthenticationService(CreateTicket(Now.AddSeconds(30)));
        var refresh = new FakeRefreshTokenClient
        {
            Result = new("access-new", "refresh-new", Now.AddHours(1))
        };
        var context = CreateContext(authentication);
        var api = new RecordingHandler();
        var handler = new WebApiBearerTokenHandler(
            new HttpContextAccessor { HttpContext = context },
            CreateService(refresh))
        {
            InnerHandler = api
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test/patients");
        request.Headers.Authorization = new("Bearer", "access-old");

        await new HttpClient(handler).SendAsync(request);

        Assert.Equal("access-new", api.BearerToken);
    }

    [Fact]
    public async Task ReauthenticationMiddlewareChallengesPageRequestsWithoutDetails()
    {
        var authentication = new FakeAuthenticationService(CreateTicket(Now));
        var context = CreateContext(authentication);
        context.Request.Path = "/Patients/Search";
        var middleware = new WebSessionReauthenticationMiddleware(
            _ => throw new WebSessionReauthenticationRequiredException());

        await middleware.InvokeAsync(context);

        Assert.Equal(1, authentication.ChallengeCount);
        Assert.Equal("/Patients/Search", authentication.ChallengeProperties!.RedirectUri);
    }

    [Fact]
    public async Task ReauthenticationMiddlewareReturns401ForAjaxWithoutChallenge()
    {
        var authentication = new FakeAuthenticationService(CreateTicket(Now));
        var context = CreateContext(authentication);
        context.Request.Headers.XRequestedWith = "XMLHttpRequest";
        var middleware = new WebSessionReauthenticationMiddleware(
            _ => throw new WebSessionReauthenticationRequiredException());

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal(0, authentication.ChallengeCount);
    }

    private static WebSessionTokenService CreateService(FakeRefreshTokenClient refresh) =>
        new(
            refresh,
            new SessionTokenRefreshCoordinator(new FixedTimeProvider(Now)),
            Options.Create(new WebTokenRefreshOptions { RefreshThreshold = TimeSpan.FromMinutes(1) }),
            new FixedTimeProvider(Now),
            NullLogger<WebSessionTokenService>.Instance);

    private static DefaultHttpContext CreateContext(FakeAuthenticationService authentication)
    {
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authentication)
            .BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services };
    }

    private static AuthenticationTicket CreateTicket(DateTimeOffset expiresAt)
    {
        var properties = new AuthenticationProperties { ExpiresUtc = expiresAt };
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = "access-old" },
            new AuthenticationToken { Name = "refresh_token", Value = "refresh-old" },
            new AuthenticationToken
            {
                Name = "expires_at",
                Value = expiresAt.ToString("O", CultureInfo.InvariantCulture)
            }
        ]);
        return new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")], "test")),
            properties,
            CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeRefreshTokenClient : IRefreshTokenClient
    {
        public int CallCount { get; private set; }
        public RefreshedTokenSet Result { get; set; } = new("access-new", "refresh-new", Now.AddHours(1));
        public Exception? Exception { get; set; }

        public Task<RefreshedTokenSet> RedeemAsync(string refreshToken, CancellationToken cancellationToken)
        {
            CallCount++;
            if (Exception is not null)
            {
                throw Exception;
            }

            Assert.Equal("refresh-old", refreshToken);
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeAuthenticationService(AuthenticationTicket ticket) : IAuthenticationService
    {
        public int SignInCount { get; private set; }
        public int SignOutCount { get; private set; }
        public int ChallengeCount { get; private set; }
        public AuthenticationProperties? SignedInProperties { get; private set; }
        public AuthenticationProperties? ChallengeProperties { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.Success(ticket));

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            ChallengeCount++;
            ChallengeProperties = properties;
            return Task.CompletedTask;
        }

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
        {
            SignInCount++;
            SignedInProperties = properties;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOutCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? BearerToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            BearerToken = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
