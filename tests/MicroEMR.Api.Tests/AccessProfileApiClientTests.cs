using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Services.TenantUserAdministration;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class AccessProfileApiClientTests
{
    [Theory]
    [InlineData("override")]
    [InlineData("assignment")]
    [InlineData("profile")]
    public async Task MissingMutationThrowsInsteadOfReportingSuccess(string operation)
    {
        using var handler = new Handler(HttpStatusCode.NotFound);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };
        using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(new Authentication()).BuildServiceProvider();
        var client = Client(http, services);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => operation switch
        {
            "override" => client.SetUserOverrideAsync("user", PermissionKeys.ClinicalDataManage, PermissionOverrideState.Deny, "AAAAAAAAAAE="),
            "assignment" => client.AssignAsync("user", Guid.NewGuid(), "AAAAAAAAAAE="),
            _ => client.UpdateAsync(Guid.NewGuid(), [PermissionKeys.PatientsView], "AAAAAAAAAAE=")
        });

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task MissingAccessReadStillReturnsNull()
    {
        using var handler = new Handler(HttpStatusCode.NotFound);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };
        using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(new Authentication()).BuildServiceProvider();
        Assert.Null(await Client(http, services).GetUserAccessAsync("user"));
    }

    [Fact]
    public async Task SuccessfulDenySendsSelectedPermissionAndConcurrencyVersion()
    {
        using var handler = new Handler(HttpStatusCode.NoContent);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };
        using var services = new ServiceCollection().AddSingleton<IAuthenticationService>(new Authentication()).BuildServiceProvider();
        await Client(http, services).SetUserOverrideAsync("user", PermissionKeys.ClinicalDataManage, PermissionOverrideState.Deny, "AAAAAAAAAAE=");

        Assert.Equal(HttpMethod.Put, handler.Method);
        Assert.Equal("/api/admin/access-profiles/users/user/overrides/ClinicalData.Manage", handler.Path);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("Deny", body.RootElement.GetProperty("overrideState").GetString());
        Assert.Equal("AAAAAAAAAAE=", body.RootElement.GetProperty("rowVersion").GetString());
    }

    private static AccessProfileApiClient Client(HttpClient http, IServiceProvider services) => new(http,
        new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = services } });

    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri!.AbsolutePath;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status);
        }
    }

    private sealed class Authentication : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "test-token" }]);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity("test")), properties, "test")));
        }
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
