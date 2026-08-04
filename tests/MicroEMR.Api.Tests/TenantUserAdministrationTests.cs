using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PlatformAdministration;
using MicroEMR.Application.Tenancy;
using MicroEMR.Application.TenantUserAdministration;
using MicroEMR.Web.Services.TenantUserAdministration;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TenantUserAdministrationTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task ServiceComposesMultipleUsersRolesStatusesAndExactClinicalMappings()
    {
        var service = Service(
            [Membership("auth-1", TenantA, "Active", "ClinicAdministrator", "Physician"),
             Membership("auth-2", TenantA, "Suspended", "Nurse")],
            [Profile("auth-1", "admin", "Admin User"), Profile("auth-2", "nurse", "Nurse User")],
            [new ClinicalUser(17, Guid.NewGuid(), "different-clinical-name", "Clinical", true, "auth-1")]);

        var users = await service.GetTenantUsersAsync();

        Assert.Equal(2, users.Count);
        var admin = Assert.Single(users, x => x.AuthUserId == "auth-1");
        Assert.Equal(["ClinicAdministrator", "Physician"], admin.TenantRoles);
        Assert.Equal("Active", admin.MembershipStatus);
        Assert.True(admin.ClinicalUserProvisioned);
        Assert.Equal(17, admin.ClinicalUserId);
        Assert.True(admin.ClinicalUserActive);
        var nurse = Assert.Single(users, x => x.AuthUserId == "auth-2");
        Assert.Equal("Suspended", nurse.MembershipStatus);
        Assert.False(nurse.ClinicalUserProvisioned);
        Assert.Null(nurse.ClinicalUserId);
    }

    [Fact]
    public async Task ServiceExcludesOtherTenantEvenForSameAuthUser()
    {
        var service = Service(
            [Membership("shared", TenantA, "Active", "ClinicAdministrator"),
             Membership("shared", TenantB, "Active", "Physician"),
             Membership("tenant-b-only", TenantB, "Active", "Nurse")],
            [Profile("shared", "shared", "Shared User"), Profile("tenant-b-only", "other", "Other")],
            []);

        var user = Assert.Single(await service.GetTenantUsersAsync());
        Assert.Equal("shared", user.AuthUserId);
        Assert.Equal(["ClinicAdministrator"], user.TenantRoles);
    }

    [Fact]
    public async Task ProvisioningNeverFallsBackToMatchingUsernameOrEmail()
    {
        var service = Service(
            [Membership("real-subject", TenantA, "Active", "ClinicAdministrator")],
            [new IdentityUserProfile("real-subject", "same-name", "User", "same@example.test", true)],
            [new ClinicalUser(9, Guid.NewGuid(), "same-name", "User", true, "different-subject")]);

        var user = Assert.Single(await service.GetTenantUsersAsync());
        Assert.False(user.ClinicalUserProvisioned);
        Assert.Null(user.ClinicalUserId);
    }

    [Fact]
    public void ApiAndWebControllersRequireTenantClinicAdministratorPolicy()
    {
        Assert.Equal(TenantAuthorizationPolicies.ClinicAdministrator, Policy(typeof(MicroEMR.Api.Controllers.TenantUserAdministrationController)));
        Assert.Equal("TenantClinicAdministrator", Policy(typeof(MicroEMR.Web.Controllers.TenantUserAdministrationController)));
    }

    [Fact]
    public async Task WebClientUsesReadOnlyTenantScopedEndpoint()
    {
        var handler = new RecordingHandler();
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(new TestAuthenticationService()).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        var client = new TenantUserAdministrationApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") },
            new HttpContextAccessor { HttpContext = context });

        await client.GetUsersAsync();

        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("api/admin/users", handler.Uri);
        Assert.Null(handler.Body);
    }

    private static string? Policy(Type type) => Assert.Single(type
        .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Policy;

    private static ITenantUserAdministrationService Service(
        IReadOnlyList<PlatformMembershipInfo> memberships,
        IReadOnlyList<IdentityUserProfile> profiles,
        IReadOnlyList<ClinicalUser> clinicalUsers) =>
        new TenantUserAdministrationService(
            new TenantContext(TenantA, "tenant-a", "Tenant A"),
            new MembershipService(memberships),
            new IdentityLookup(profiles),
            new ClinicalLookup(clinicalUsers),
            NullLogger<TenantUserAdministrationService>.Instance);

    private static PlatformMembershipInfo Membership(string user, Guid tenant, string status, params string[] roles) =>
        new(user, tenant, tenant == TenantA ? "tenant-a" : "tenant-b", tenant == TenantA ? "Tenant A" : "Tenant B", status, false, roles);
    private static IdentityUserProfile Profile(string id, string username, string name) =>
        new(id, username, name, $"{username}@example.test", true);

    private sealed class MembershipService(IReadOnlyList<PlatformMembershipInfo> values) : IPlatformMembershipAdministrationService
    {
        public Task<IReadOnlyList<PlatformMembershipInfo>> GetTenantMembershipsAsync(Guid tenantUid, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlatformMembershipInfo>>(values.Where(x => x.TenantUid == tenantUid).ToArray());
        public Task<IReadOnlyList<PlatformMembershipInfo>> GetMembershipsAsync(string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddMembershipAsync(AddUserTenantMembershipRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetMembershipStatusAsync(SetUserTenantMembershipStatusRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetDefaultAsync(SetDefaultTenantRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRoleAsync(AddUserTenantRoleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveRoleAsync(RemoveUserTenantRoleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class IdentityLookup(IReadOnlyList<IdentityUserProfile> values) : IIdentityUserProfileLookup
    {
        public Task<IdentityUserProfile?> GetByIdAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(values.SingleOrDefault(x => x.UserId == userId));
    }

    private sealed class ClinicalLookup(IReadOnlyList<ClinicalUser> values) : IClinicalUserRepository
    {
        public Task<ClinicalUser?> GetByAuthSubjectIdAsync(string authSubjectId, CancellationToken cancellationToken = default) =>
            Task.FromResult(values.SingleOrDefault(x => x.AuthSubjectId == authSubjectId));
        public Task<ClinicalUser> SetAuthSubjectIdAsync(long userId, string authSubjectId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ClinicalUser> ProvisionAsync(string authSubjectId, string username, string displayName, string? email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Uri { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri!.PathAndQuery.TrimStart('/');
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("[]", Encoding.UTF8, "application/json") };
        }
    }

    private sealed class TestAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "token" }]);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity("test")), properties, "test")));
        }
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
