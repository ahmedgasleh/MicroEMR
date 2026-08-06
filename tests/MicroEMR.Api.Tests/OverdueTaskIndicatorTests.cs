using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Web.Controllers;
using MicroEMR.Web.Services.PatientTasks;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class OverdueTaskIndicatorTests
{
    [Fact]
    public async Task WebClientCallsAuthenticatedCountEndpointWithoutUserOrTenantIdentifiers()
    {
        var handler = new Handler(HttpStatusCode.OK, "{\"count\":3}");
        var client = new PatientTaskApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") },
            new HttpContextAccessor { HttpContext = Context() });

        Assert.Equal(3, await client.GetOverdueCountAsync());
        Assert.Equal("api/patient-tasks/overdue/count", handler.Path);
        Assert.Equal("Bearer test-token", handler.Authorization);
        Assert.DoesNotContain("user", handler.Path, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenant", handler.Path, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task WebClientPreservesAuthorizationFailureWithoutReturningMisleadingCount(HttpStatusCode status)
    {
        var client = new PatientTaskApiClient(
            new HttpClient(new Handler(status, "")) { BaseAddress = new Uri("https://api.test/") },
            new HttpContextAccessor { HttpContext = Context() });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetOverdueCountAsync());
        Assert.Equal(status, exception.StatusCode);
    }

    [Fact]
    public void IndicatorIsAuthenticatedPhiFreeAndLinksToExistingOpenTasksWorkflow()
    {
        var layout = Read("src", "MicroEMR.Web", "Views", "Shared", "_AppLayout.cshtml");
        Assert.NotEmpty(typeof(PatientTasksController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.Contains("id=\"overdueTaskIndicator\"", layout);
        Assert.Contains("asp-fragment=\"openTasksHeading\"", layout);
        Assert.Contains("class=\"topbar-icon-button d-none\"", layout);
        Assert.Contains("aria-label=\"Overdue tasks\"", layout);
        Assert.DoesNotContain("PatientDisplayName", layout);
        Assert.DoesNotContain("TaskTitle", layout);
        Assert.DoesNotContain("DueAt", layout);
    }

    [Fact]
    public void ScriptHidesZeroAndFailuresAndShowsAccessiblePositiveCountWithoutCaching()
    {
        var script = Read("src", "MicroEMR.Web", "ClientApp", "overdue-task-indicator.ts");
        Assert.Contains("if (!response.ok) return", script);
        Assert.Contains("value.count <= 0", script);
        Assert.Contains("classList.remove(\"d-none\")", script);
        Assert.Contains("overdue task", script);
        Assert.Contains("credentials: \"same-origin\"", script);
        Assert.DoesNotContain("localStorage", script);
        Assert.DoesNotContain("sessionStorage", script);
        Assert.DoesNotContain("setInterval", script);
        Assert.DoesNotContain("TenantUid", script);
        Assert.DoesNotContain("userId", script);
    }

    [Fact]
    public void ExistingDashboardTasksAndResponsiveNavigationRemainInPlaceWithoutNotificationCenter()
    {
        var dashboard = Read("src", "MicroEMR.Web", "Views", "Home", "Index.cshtml");
        var layout = Read("src", "MicroEMR.Web", "Views", "Shared", "_AppLayout.cshtml");
        var sidebar = Read("src", "MicroEMR.Web", "Views", "Shared", "_Sidebar.cshtml");
        Assert.Contains("id=\"openTasksHeading\"", dashboard);
        Assert.Contains("Model.OpenTasks", dashboard);
        Assert.Contains("mobileSidebarButton", layout);
        Assert.Contains("appSidebar", sidebar);
        Assert.DoesNotContain("notification-dropdown", layout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("notification inbox", layout, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string Path { get; private set; } = string.Empty;
        public string? Authorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Path = request.RequestUri!.PathAndQuery.TrimStart('/');
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private static DefaultHttpContext Context()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "test-token" }]);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user")], "test")), properties, "test");
        return new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService>(new Auth(ticket)).BuildServiceProvider() };
    }

    private sealed class Auth(AuthenticationTicket ticket) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext c, string? s) => Task.FromResult(AuthenticateResult.Success(ticket));
        public Task ChallengeAsync(HttpContext c, string? s, AuthenticationProperties? p) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext c, string? s, AuthenticationProperties? p) => Task.CompletedTask;
        public Task SignInAsync(HttpContext c, string? s, ClaimsPrincipal p, AuthenticationProperties? x) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext c, string? s, AuthenticationProperties? p) => Task.CompletedTask;
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string file = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", ".."));
}
