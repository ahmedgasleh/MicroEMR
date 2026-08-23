using System.Security.Claims;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Tenancy;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class AuthenticatedClinicalUserAccessorTests
{
    [Theory]
    [InlineData("56c3a21b-3207-4954-a970-f32f1feb2ac2", 1)]
    [InlineData("opaque-auth-subject", 73)]
    public async Task OpaqueSubjectResolvesClinicalUser(string subject, long userId)
    {
        var repository = new StubRepository(subject, User(userId, subject));
        var accessor = Accessor(Principal(subject), repository, Guid.NewGuid());

        Assert.Equal(userId, await accessor.GetRequiredUserIdAsync());
        Assert.Equal(subject, repository.RequestedSubject);
    }

    [Fact]
    public async Task MissingEmptyUnknownInactiveAndUnauthenticatedUsersFail()
    {
        await Assert.ThrowsAsync<ClinicalUserResolutionException>(() =>
            Accessor(new ClaimsPrincipal(new ClaimsIdentity([], "Test")), new StubRepository(), Guid.NewGuid())
                .GetRequiredUserIdAsync());
        await Assert.ThrowsAsync<ClinicalUserResolutionException>(() =>
            Accessor(Principal(""), new StubRepository(), Guid.NewGuid()).GetRequiredUserIdAsync());
        var missing = await Assert.ThrowsAsync<ClinicalUserResolutionException>(() =>
            Accessor(Principal("unknown"), new StubRepository(), Guid.NewGuid()).GetRequiredUserIdAsync());
        var inactive = await Assert.ThrowsAsync<ClinicalUserResolutionException>(() =>
            Accessor(Principal("inactive"), new StubRepository("inactive", User(8, "inactive", false)), Guid.NewGuid())
                .GetRequiredUserIdAsync());
        Assert.True(missing.IsCompletedUnresolved);
        Assert.True(inactive.IsCompletedUnresolved);
        await Assert.ThrowsAsync<ClinicalUserResolutionException>(() =>
            Accessor(new ClaimsPrincipal(new ClaimsIdentity()), new StubRepository(), Guid.NewGuid())
                .GetRequiredUserIdAsync());
    }

    [Fact]
    public async Task MissingTenantFailsBeforeLookup()
    {
        var repository = new StubRepository("subject", User(1, "subject"));
        await Assert.ThrowsAsync<ClinicalUserResolutionException>(() =>
            Accessor(Principal("subject"), repository, Guid.Empty).GetRequiredUserIdAsync());
        Assert.Null(repository.RequestedSubject);
    }

    [Fact]
    public async Task UnresolvedTenantFailsBeforeLookup()
    {
        var repository = new StubRepository("subject", User(1, "subject"));
        var context = new DefaultHttpContext { User = Principal("subject") };
        var accessor = new AuthenticatedClinicalUserAccessor(
            new HttpContextAccessor { HttpContext = context },
            new TenantContextAccessor(),
            repository);

        await Assert.ThrowsAsync<ClinicalUserResolutionException>(
            () => accessor.GetRequiredUserIdAsync());
        Assert.Null(repository.RequestedSubject);
    }

    [Fact]
    public async Task ResolutionIsTenantScopedAndSameSubjectCanMapDifferently()
    {
        const string subject = "shared-subject";
        var tenantA = Accessor(Principal(subject), new StubRepository(subject, User(14, subject)), Guid.NewGuid());
        var tenantB = Accessor(Principal(subject), new StubRepository(subject, User(52, subject)), Guid.NewGuid());

        Assert.Equal(14, await tenantA.GetRequiredUserIdAsync());
        Assert.Equal(52, await tenantB.GetRequiredUserIdAsync());
    }

    private static AuthenticatedClinicalUserAccessor Accessor(
        ClaimsPrincipal principal,
        IClinicalUserRepository repository,
        Guid tenantUid)
    {
        var context = new DefaultHttpContext { User = principal };
        var tenant = new TenantContextAccessor();
        tenant.SetTenant(new StubTenantContext(tenantUid));
        return new(
            new HttpContextAccessor { HttpContext = context },
            tenant,
            repository);
    }

    private static ClaimsPrincipal Principal(string subject) =>
        new(new ClaimsIdentity([new Claim("sub", subject)], "Test"));

    private static ClinicalUser User(long id, string subject, bool active = true) =>
        new(id, Guid.NewGuid(), "user", "User", active, subject);

    private sealed class StubRepository(string? subject = null, ClinicalUser? user = null)
        : IClinicalUserRepository
    {
        public string? RequestedSubject { get; private set; }
        public Task<ClinicalUser?> GetByAuthSubjectIdAsync(string authSubjectId, CancellationToken cancellationToken = default)
        {
            RequestedSubject = authSubjectId;
            return Task.FromResult(string.Equals(subject, authSubjectId, StringComparison.Ordinal) ? user : null);
        }
        public Task<ClinicalUser> SetAuthSubjectIdAsync(long userId, string authSubjectId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ClinicalUser> ProvisionAsync(string authSubjectId, string username, string displayName, string? email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed record StubTenantContext(Guid TenantUid) : ITenantContext
    {
        public string TenantKey => "tenant";
        public string DisplayName => "Tenant";
    }
}
