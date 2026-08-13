using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PlatformAdministration;
using MicroEMR.Application.Tenancy;
using MicroEMR.Application.TenantUserAdministration;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class AddTenantUserWorkflowTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void MigrationUsesExistingTenantRoleSchema()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var sql = File.ReadAllText(Path.Combine(root, "db", "platform", "009_tenant_user_creation.sql"));
        Assert.Contains("INSERT dbo.UserTenantRole(UserId,TenantUid,RoleName)", sql);
        Assert.DoesNotContain("AssignedAt", sql);
    }

    [Fact]
    public async Task NewIdentityCreatesTenantAccessRoleAndClinicalUser()
    {
        var harness = new Harness(created: true);
        var result = await harness.Service.AddTenantUserAsync(new("Jane", "Doe", "jane@example.test", "Physician", true));
        Assert.True(result.AuthIdentityCreated);
        Assert.False(result.ClinicalProvisioningFailed);
        Assert.Equal(("auth-jane", Tenant, "Physician", "admin-subject"), harness.Creation.Last);
        Assert.Equal(1, harness.Clinical.ProvisionCalls);
        Assert.Equal(["Physician"], result.User.TenantRoles);
    }

    [Fact]
    public async Task ExistingIdentityIsReusedWithoutClinicalProvisioningWhenNotSelected()
    {
        var harness = new Harness(created: false);
        var result = await harness.Service.AddTenantUserAsync(new("Ignored", "Name", "jane@example.test", "Scheduler", false));
        Assert.False(result.AuthIdentityCreated);
        Assert.Equal(1, harness.Identity.Calls);
        Assert.Equal(0, harness.Clinical.ProvisionCalls);
        Assert.Equal("Scheduler", Assert.Single(result.User.TenantRoles));
    }

    [Fact]
    public async Task ExistingCurrentTenantMembershipReturnsControlledDuplicate()
    {
        var harness = new Harness(created: false, alreadyMember: true);
        var error = await Assert.ThrowsAsync<TenantMembershipAlreadyExistsException>(() =>
            harness.Service.AddTenantUserAsync(new("Jane", "Doe", "jane@example.test", "Nurse", true)));
        Assert.Equal("This user already belongs to this clinic.", error.Message);
        Assert.Null(harness.Creation.Last);
        Assert.Equal(0, harness.Clinical.ProvisionCalls);
    }

    [Fact]
    public async Task InvalidRoleIsRejectedBeforeIdentityResolution()
    {
        var harness = new Harness(created: true);
        await Assert.ThrowsAsync<TenantRoleValidationException>(() =>
            harness.Service.AddTenantUserAsync(new("Jane", "Doe", "jane@example.test", "SuperUser", true)));
        Assert.Equal(0, harness.Identity.Calls);
    }

    [Fact]
    public async Task ClinicalFailureReportsRecoverablePartialResultWithoutRecreatingMembership()
    {
        var harness = new Harness(created: true, failClinical: true);
        var result = await harness.Service.AddTenantUserAsync(new("Jane", "Doe", "jane@example.test", "Nurse", true));
        Assert.True(result.ClinicalProvisioningFailed);
        Assert.Contains("Retry", result.Message);
        Assert.NotNull(harness.Creation.Last);
        Assert.False(result.User.ClinicalUserProvisioned);
    }

    private sealed class Harness
    {
        public IdentityAdmin Identity { get; }
        public CreationRepository Creation { get; } = new();
        public ClinicalRepository Clinical { get; }
        public TenantUserAdministrationService Service { get; }

        public Harness(bool created, bool alreadyMember = false, bool failClinical = false)
        {
            var profile = new IdentityUserProfile("auth-jane", "jane@example.test", "Jane Doe", "jane@example.test", true);
            Identity = new(profile, created);
            Clinical = new(failClinical);
            var memberships = new Memberships(profile, Creation, alreadyMember);
            Service = new(new TenantContext(Tenant, "clinic", "Clinic"), memberships, new Profiles(profile), Clinical,
                new Lifecycle(), new Roles(), new Subject(), Identity, Creation,
                NullLogger<TenantUserAdministrationService>.Instance);
        }
    }

    private sealed class IdentityAdmin(IdentityUserProfile profile, bool created) : IIdentityUserAdministration
    {
        public int Calls { get; private set; }
        public Task<ResolveOrCreateIdentityResult> ResolveOrCreateAsync(ResolveOrCreateIdentityRequest request, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(new ResolveOrCreateIdentityResult(profile, created)); }
    }
    private sealed class CreationRepository : ITenantUserCreationRepository
    {
        public (string, Guid, string, string)? Last { get; private set; }
        public Task CreateAsync(string authUserId, Guid tenantUid, string initialRole, string actorAuthUserId, CancellationToken cancellationToken = default)
        { Last = (authUserId, tenantUid, initialRole, actorAuthUserId); return Task.CompletedTask; }
    }
    private sealed class Memberships(IdentityUserProfile profile, CreationRepository creation, bool existing) : IPlatformMembershipAdministrationService
    {
        public Task<IReadOnlyList<PlatformMembershipInfo>> GetTenantMembershipsAsync(Guid tenantUid, CancellationToken cancellationToken = default)
        {
            var present = existing || creation.Last is not null;
            IReadOnlyList<PlatformMembershipInfo> result = present
                ? [new(profile.UserId, Tenant, "clinic", "Clinic", "Active", false,
                    [creation.Last?.Item3 ?? "Nurse"], DateTimeOffset.UtcNow, "AAAAAAAAAAE=")]
                : [];
            return Task.FromResult(result);
        }
        public Task<IReadOnlyList<PlatformMembershipInfo>> GetMembershipsAsync(string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddMembershipAsync(AddUserTenantMembershipRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetMembershipStatusAsync(SetUserTenantMembershipStatusRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetDefaultAsync(SetDefaultTenantRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddRoleAsync(AddUserTenantRoleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveRoleAsync(RemoveUserTenantRoleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Profiles(IdentityUserProfile profile) : IIdentityUserProfileLookup
    { public Task<IdentityUserProfile?> GetByIdAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<IdentityUserProfile?>(profile); }
    private sealed class ClinicalRepository(bool fail) : IClinicalUserRepository
    {
        private ClinicalUser? _user;
        public int ProvisionCalls { get; private set; }
        public Task<ClinicalUser?> GetByAuthSubjectIdAsync(string authSubjectId, CancellationToken cancellationToken = default) => Task.FromResult(_user);
        public Task<ClinicalUser> SetAuthSubjectIdAsync(long userId, string authSubjectId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ClinicalUser> ProvisionAsync(string authSubjectId, string username, string displayName, string? email, CancellationToken cancellationToken = default)
        {
            ProvisionCalls++;
            if (fail) throw new ClinicalUserProvisioningConflictException("test failure");
            return Task.FromResult(_user ??= new(10, Guid.NewGuid(), username, displayName, true, authSubjectId));
        }
    }
    private sealed class Lifecycle : ITenantMembershipLifecycleRepository
    {
        public Task<TenantMembershipLifecycleResult> DeactivateAsync(string authUserId, Guid tenantUid, string rowVersion, string actorAuthUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TenantMembershipLifecycleResult> ActivateAsync(string authUserId, Guid tenantUid, string rowVersion, string actorAuthUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Roles : ITenantRoleManagementRepository
    { public Task<TenantRoleUpdateResult> ReplaceRolesAsync(string authUserId, Guid tenantUid, IReadOnlyCollection<string> roles, string rowVersion, string actorAuthUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
    private sealed class Subject : IAuthenticatedSubjectAccessor { public string GetRequiredSubject() => "admin-subject"; }
}
