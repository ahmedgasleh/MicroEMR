using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.PlatformEntitlements;
using MicroEMR.Application.Security;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Application.TenantUserAdministration;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class PlatformSecurityAuditUnauthorizedApiTests
{
    [Theory]
    [InlineData("PlatformAdministrator", false)]
    [InlineData("Administrator", true)]
    public async Task AuthenticatedRoleOnlyUserReceivesForbiddenWithoutDisclosure(
        string role,
        bool includeTenantClaim)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var review = new TrackingReviewService();
        builder.Services.AddSingleton(review);
        builder.Services.AddSingleton<IPlatformSecurityAuditReviewService>(review);
        builder.Services.AddSingleton<IAuthenticatedSubjectAccessor>(new Subject());
        builder.Services.AddSingleton(new TestIdentity(role, includeTenantClaim));
        builder.Services.AddAuthentication("Step23BTest")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Step23BTest", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy(
            PlatformEntitlementPolicies.SecurityAuditView,
            policy => policy.RequireAuthenticatedUser().AddRequirements(
                new PlatformEntitlementRequirement(PlatformEntitlementKeys.SecurityAuditView))));
        builder.Services.AddSingleton<IAuthorizationHandler, PlatformEntitlementAuthorizationHandler>();
        builder.Services.AddControllers().AddApplicationPart(typeof(PlatformSecurityAuditController).Assembly);

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();

        var address = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        using var response = await client.PostAsJsonAsync(
            "api/platform/security-audit/search",
            new SecurityAuditSearchRequest());
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, review.SearchCalls);
        Assert.DoesNotContain("SecurityAuditEventUid", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Items", body, StringComparison.Ordinal);

        await app.StopAsync();
    }

    private sealed record TestIdentity(string Role, bool IncludeTenantClaim);

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestIdentity identity)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new("sub", "step23b-unauthorized-user"),
                new(ClaimTypes.Role, identity.Role)
            };
            if (identity.IncludeTenantClaim)
                claims.Add(new Claim(MicroEmrClaimTypes.TenantId, Guid.NewGuid().ToString("D")));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed class TrackingReviewService : IPlatformSecurityAuditReviewService
    {
        public int SearchCalls { get; private set; }

        public Task<SecurityAuditSearchPage> SearchAsync(
            SecurityAuditSearchRequest request,
            string actorSubject,
            Guid correlationId,
            CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            return Task.FromResult(new SecurityAuditSearchPage(
                [], null, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, 25));
        }

        public Task<SecurityAuditDetail?> GetByUidAsync(
            Guid securityAuditEventUid,
            string actorSubject,
            Guid correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SecurityAuditDetail?>(null);
    }

    private sealed class Subject : IAuthenticatedSubjectAccessor
    {
        public string GetRequiredSubject() => "step23b-unauthorized-user";
    }
}
