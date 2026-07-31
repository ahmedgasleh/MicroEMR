using MicroEMR.Application.Tenancy;
using MicroEMR.Auth.Data;
using MicroEMR.Auth.Services.Tenancy;
using Xunit;

namespace MicroEMR.Auth.Tests;

public sealed class UserTenantResolverTests
{
    [Fact]
    public async Task OneMembership_ResolvesAutomatically()
    {
        var membership = Membership(Guid.NewGuid(), false);
        var result = await Resolver([membership]).ResolveAsync(User());
        Assert.Equal(TenantMembershipResolutionStatus.Resolved, result.Status);
        Assert.Equal(membership.TenantUid, result.Membership?.TenantUid);
    }

    [Fact]
    public async Task MultipleMemberships_OneDefault_ResolvesDefault()
    {
        var regular = Membership(Guid.NewGuid(), false);
        var preferred = Membership(Guid.NewGuid(), true);
        var result = await Resolver([regular, preferred]).ResolveAsync(User());
        Assert.Equal(TenantMembershipResolutionStatus.Resolved, result.Status);
        Assert.Equal(preferred.TenantUid, result.Membership?.TenantUid);
    }

    [Fact]
    public async Task MultipleMemberships_NoDefault_RequiresSelection()
    {
        var result = await Resolver([Membership(Guid.NewGuid(), false), Membership(Guid.NewGuid(), false)])
            .ResolveAsync(User());
        Assert.Equal(TenantMembershipResolutionStatus.SelectionRequired, result.Status);
        Assert.Equal(2, result.AvailableMemberships.Count);
    }

    [Fact]
    public async Task MultipleDefaults_FailsClosed() =>
        await Assert.ThrowsAsync<InvalidTenantMembershipDataException>(() =>
            Resolver([Membership(Guid.NewGuid(), true), Membership(Guid.NewGuid(), true)]).ResolveAsync(User()));

    [Fact]
    public async Task NoMemberships_DoesNotResolve()
    {
        var result = await Resolver([]).ResolveAsync(User());
        Assert.Equal(TenantMembershipResolutionStatus.None, result.Status);
    }

    private static UserTenantResolver Resolver(IReadOnlyList<UserTenantMembershipInfo> memberships) =>
        new(new StubMembershipService(memberships));

    private static ApplicationUser User() => new() { Id = "user-1" };

    private static UserTenantMembershipInfo Membership(Guid tenantUid, bool isDefault) =>
        new("user-1", tenantUid, "clinic", "Clinic", "Active", isDefault, ["Clinician"]);

    private sealed class StubMembershipService(IReadOnlyList<UserTenantMembershipInfo> memberships)
        : IUserTenantMembershipService
    {
        public Task<IReadOnlyList<UserTenantMembershipInfo>> GetActiveMembershipsAsync(
            ApplicationUser user, CancellationToken cancellationToken = default) => Task.FromResult(memberships);

        public Task<UserTenantMembershipInfo?> GetDefaultMembershipAsync(
            ApplicationUser user, CancellationToken cancellationToken = default) =>
            Task.FromResult(memberships.SingleOrDefault(m => m.IsDefaultTenant));
    }
}
