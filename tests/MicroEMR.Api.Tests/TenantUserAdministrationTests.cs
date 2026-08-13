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
    public async Task DetailsReturnSelectedCurrentTenantUserWithRolesAndClinicalMapping()
    {
        var service = Service(
            [Membership("auth-1", TenantA, "Active", "ClinicAdministrator"),
             Membership("auth-2", TenantA, "Inactive", "Nurse")],
            [Profile("auth-1", "admin", "Admin"), Profile("auth-2", "nurse", "Nurse User")],
            [new ClinicalUser(42, Guid.NewGuid(), "nurse", "Nurse User", true, "auth-2")]);

        var user = await service.GetTenantUserAsync("auth-2");

        Assert.NotNull(user);
        Assert.Equal("Inactive", user.MembershipStatus);
        Assert.Equal(["Nurse"], user.TenantRoles);
        Assert.Equal(42, user.ClinicalUserId);
    }

    [Fact]
    public async Task DetailsDoNotReturnUserOutsideCurrentTenant()
    {
        var service = Service(
            [Membership("tenant-b-user", TenantB, "Active", "ClinicAdministrator")],
            [Profile("tenant-b-user", "other", "Other")], []);

        Assert.Null(await service.GetTenantUserAsync("tenant-b-user"));
        Assert.Null(await service.GetTenantUserAsync("missing"));
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

    [Fact]
    public void RoleUpdateContractAcceptsOnlyRolesAndRowVersion()
    {
        var properties = typeof(TenantRoleUpdateRequest).GetProperties().Select(x => x.Name).Order().ToArray();
        Assert.Equal(["RowVersion", "SelectedRoles"], properties);
    }

    [Fact]
    public async Task UnknownAndEmptyRoleSetsAreRejectedBeforePersistence()
    {
        var repository = new RecordingRoleRepository();
        var service = new TenantUserAdministrationService(
            new TenantContext(TenantA, "tenant-a", "Tenant A"),
            new MembershipService([Membership("auth-2", TenantA, "Active", "Nurse")]),
            new IdentityLookup([Profile("auth-2", "nurse", "Nurse")]), new ClinicalLookup([]),
            new LifecycleRepository(), repository, new SubjectAccessor("auth-1"),
            NullLogger<TenantUserAdministrationService>.Instance);

        await Assert.ThrowsAsync<TenantRoleValidationException>(() =>
            service.UpdateTenantRolesAsync("auth-2", ["NotARole"], "AAAAAAAAAAE="));
        await Assert.ThrowsAsync<TenantRoleValidationException>(() =>
            service.UpdateTenantRolesAsync("auth-2", [], "AAAAAAAAAAE="));
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public void RoleProcedureIsAtomicTenantScopedConcurrentAuditedAndAdminSafe()
    {
        var sql = File.ReadAllText(Path.Combine(RepositoryRoot(), "db", "platform", "008_tenant_role_management.sql"));
        Assert.Contains("WITH(UPDLOCK,HOLDLOCK)", sql);
        Assert.Contains("@CurrentRowVersion<>@ExpectedRowVersion", sql);
        Assert.Contains("m.TenantUid=@TenantUid", sql);
        Assert.Contains("m.MembershipStatus='Active'", sql);
        Assert.Contains("m.UserId<>@UserId", sql);
        Assert.Contains("@UserId=@ActorUserId", sql);
        Assert.Contains("TenantRolesReplaced", sql);
        Assert.Contains("BEGIN TRANSACTION", sql);
        Assert.DoesNotContain("AspNetRoles", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActiveTenantMemberProvisioningUsesExactSubjectAndIsIdempotent()
    {
        var clinical = new ProvisioningClinicalLookup();
        var membership = Membership("opaque-subject-42", TenantA, "Active", "Nurse");
        var service = Service([membership], [Profile("opaque-subject-42", "nurse", "Nurse User")], clinical);

        var first = await service.ProvisionClinicalUserAsync("opaque-subject-42");
        var second = await service.ProvisionClinicalUserAsync("opaque-subject-42");

        Assert.True(first.ClinicalUserProvisioned);
        Assert.Equal(first.ClinicalUserId, second.ClinicalUserId);
        Assert.Equal("opaque-subject-42", clinical.Subject);
        Assert.Equal(1, clinical.CreatedCount);
        Assert.Equal("Active", second.MembershipStatus);
        Assert.Equal(["Nurse"], second.TenantRoles);
    }

    [Theory]
    [InlineData("Inactive")]
    [InlineData("Suspended")]
    public async Task InactiveMembershipCannotBeProvisioned(string status)
    {
        var clinical = new ProvisioningClinicalLookup();
        var service = Service([Membership("auth-2", TenantA, status, "Nurse")],
            [Profile("auth-2", "nurse", "Nurse")], clinical);
        await Assert.ThrowsAsync<TenantClinicalProvisioningNotEligibleException>(() =>
            service.ProvisionClinicalUserAsync("auth-2"));
        Assert.Equal(0, clinical.CreatedCount);
    }

    [Fact]
    public async Task UserOutsideCurrentTenantCannotBeProvisioned()
    {
        var clinical = new ProvisioningClinicalLookup();
        var service = Service([Membership("auth-2", TenantB, "Active", "Nurse")],
            [Profile("auth-2", "nurse", "Nurse")], clinical);
        await Assert.ThrowsAsync<TenantMembershipNotFoundException>(() =>
            service.ProvisionClinicalUserAsync("auth-2"));
        Assert.Equal(0, clinical.CreatedCount);
    }

    [Fact]
    public async Task ProvisioningClientPostsToNarrowRouteWithNoBody()
    {
        var handler = new RecordingHandler(new TenantUserAdministrationItem("auth-2", "nurse", "Nurse", null,
            true, "Active", ["Nurse"], true, 27, true, null, "AAAAAAAAAAE=", false));
        var services = new ServiceCollection().AddSingleton<IAuthenticationService>(new TestAuthenticationService()).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        var client = new TenantUserAdministrationApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") },
            new HttpContextAccessor { HttpContext = context });

        await client.ProvisionClinicalUserAsync("auth-2");

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("api/admin/users/auth-2/clinical-user/provision", handler.Uri);
        Assert.Null(handler.Body);
    }

    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", ".."));

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
            new LifecycleRepository(),
            new RoleRepository(),
            new SubjectAccessor("auth-1"),
            NullLogger<TenantUserAdministrationService>.Instance);

    private static ITenantUserAdministrationService Service(
        IReadOnlyList<PlatformMembershipInfo> memberships,
        IReadOnlyList<IdentityUserProfile> profiles,
        IClinicalUserRepository clinicalUsers) =>
        new TenantUserAdministrationService(new TenantContext(TenantA, "tenant-a", "Tenant A"),
            new MembershipService(memberships), new IdentityLookup(profiles), clinicalUsers,
            new LifecycleRepository(), new RoleRepository(), new SubjectAccessor("auth-1"),
            NullLogger<TenantUserAdministrationService>.Instance);

    private static PlatformMembershipInfo Membership(string user, Guid tenant, string status, params string[] roles) =>
        new(user, tenant, tenant == TenantA ? "tenant-a" : "tenant-b", tenant == TenantA ? "Tenant A" : "Tenant B", status, false, roles,
            DateTimeOffset.UtcNow, "AAAAAAAAAAE=");
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

    private sealed class ProvisioningClinicalLookup : IClinicalUserRepository
    {
        private ClinicalUser? _user;
        public int CreatedCount { get; private set; }
        public string? Subject { get; private set; }
        public Task<ClinicalUser?> GetByAuthSubjectIdAsync(string authSubjectId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_user?.AuthSubjectId == authSubjectId ? _user : null);
        public Task<ClinicalUser> SetAuthSubjectIdAsync(long userId, string authSubjectId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<ClinicalUser> ProvisionAsync(string authSubjectId, string username, string displayName,
            string? email, CancellationToken cancellationToken = default)
        {
            Subject = authSubjectId;
            if (_user is null) { CreatedCount++; _user = new ClinicalUser(27, Guid.NewGuid(), username, displayName, true, authSubjectId); }
            return Task.FromResult(_user);
        }
    }

    private sealed class LifecycleRepository : ITenantMembershipLifecycleRepository
    {
        public Task<TenantMembershipLifecycleResult> DeactivateAsync(string authUserId, Guid tenantUid, string rowVersion, string actorAuthUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TenantMembershipLifecycleResult("Inactive", DateTimeOffset.UtcNow, "AAAAAAAAAAI="));
        public Task<TenantMembershipLifecycleResult> ActivateAsync(string authUserId, Guid tenantUid, string rowVersion, string actorAuthUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TenantMembershipLifecycleResult("Active", DateTimeOffset.UtcNow, "AAAAAAAAAAI="));
    }

    private sealed class RoleRepository : ITenantRoleManagementRepository
    {
        public Task<TenantRoleUpdateResult> ReplaceRolesAsync(string authUserId, Guid tenantUid,
            IReadOnlyCollection<string> roles, string rowVersion, string actorAuthUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TenantRoleUpdateResult(roles, DateTimeOffset.UtcNow, "AAAAAAAAAAI="));
    }

    private sealed class RecordingRoleRepository : ITenantRoleManagementRepository
    {
        public int Calls { get; private set; }
        public Task<TenantRoleUpdateResult> ReplaceRolesAsync(string authUserId, Guid tenantUid,
            IReadOnlyCollection<string> roles, string rowVersion, string actorAuthUserId, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(new TenantRoleUpdateResult(roles, DateTimeOffset.UtcNow, "AAAAAAAAAAI=")); }
    }

    private sealed class SubjectAccessor(string subject) : IAuthenticatedSubjectAccessor
    {
        public string GetRequiredSubject() => subject;
    }

    private sealed class RecordingHandler(TenantUserAdministrationItem? responseUser = null) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Uri { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri!.PathAndQuery.TrimStart('/');
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var json = responseUser is null ? "[]" : System.Text.Json.JsonSerializer.Serialize(responseUser,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") };
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
