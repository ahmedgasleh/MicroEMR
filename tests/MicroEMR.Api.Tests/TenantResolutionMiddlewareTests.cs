using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Api.Middleware;
using MicroEMR.Application.Security;
using MicroEMR.Application.Tenancy;
using MicroEMR.Core.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TenantResolutionMiddlewareTests
{
    private static readonly Guid TenantUid =
        Guid.Parse("0e3b6e46-baba-4d04-a9e5-1314586b9fb9");

    [Fact]
    public async Task ValidRequestUsesCatalogValuesAndClearsContext()
    {
        var accessor = new TenantContextAccessor();
        ITenantContext? observed = null;
        var middleware = Middleware(_ =>
        {
            observed = accessor.Current;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            AuthenticatedContext(TenantUid.ToString("D")),
            new StubCatalog(ActiveTenant()),
            new StubMembershipRepository(Membership()),
            accessor);

        Assert.NotNull(observed);
        Assert.Equal("platform-key", observed.TenantKey);
        Assert.Equal("Platform Display Name", observed.DisplayName);
        Assert.Null(accessor.Current);
    }

    [Fact]
    public async Task ContextIsClearedWhenEndpointThrows()
    {
        var accessor = new TenantContextAccessor();
        var middleware = Middleware(_ => throw new TestException());

        await Assert.ThrowsAsync<TestException>(() => middleware.InvokeAsync(
            AuthenticatedContext(TenantUid.ToString("D")),
            new StubCatalog(ActiveTenant()),
            new StubMembershipRepository(Membership()),
            accessor));

        Assert.Null(accessor.Current);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task MissingOrInvalidTenantClaimReturnsForbidden(string? claimValue)
    {
        var context = AuthenticatedContext(claimValue);

        await Middleware(_ => Task.CompletedTask).InvokeAsync(
            context,
            new StubCatalog(ActiveTenant()),
            new StubMembershipRepository(Membership()),
            new TenantContextAccessor());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task ConflictingTenantClaimsReturnForbidden()
    {
        var context = AuthenticatedContext(TenantUid.ToString("D"));
        ((ClaimsIdentity)context.User.Identity!).AddClaim(new Claim(
            MicroEmrClaimTypes.TenantId,
            Guid.NewGuid().ToString("D")));

        await Middleware(_ => Task.CompletedTask).InvokeAsync(
            context,
            new StubCatalog(ActiveTenant()),
            new StubMembershipRepository(Membership()),
            new TenantContextAccessor());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(TenantStatus.Provisioning)]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Archived)]
    public async Task NonActiveTenantReturnsForbidden(TenantStatus status)
    {
        var context = AuthenticatedContext(TenantUid.ToString("D"));

        await Middleware(_ => Task.CompletedTask).InvokeAsync(
            context,
            new StubCatalog(Tenant(status)),
            new StubMembershipRepository(Membership()),
            new TenantContextAccessor());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task MissingTenantOrMembershipReturnsForbidden()
    {
        var missingTenantContext = AuthenticatedContext(TenantUid.ToString("D"));
        await Middleware(_ => Task.CompletedTask).InvokeAsync(
            missingTenantContext,
            new StubCatalog(null),
            new StubMembershipRepository(Membership()),
            new TenantContextAccessor());
        Assert.Equal(StatusCodes.Status403Forbidden, missingTenantContext.Response.StatusCode);

        var missingMembershipContext = AuthenticatedContext(TenantUid.ToString("D"));
        await Middleware(_ => Task.CompletedTask).InvokeAsync(
            missingMembershipContext,
            new StubCatalog(ActiveTenant()),
            new StubMembershipRepository(null),
            new TenantContextAccessor());
        Assert.Equal(StatusCodes.Status403Forbidden, missingMembershipContext.Response.StatusCode);
    }

    [Fact]
    public async Task AnonymousEndpointIsNotBlocked()
    {
        var called = false;
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "anonymous"));

        await Middleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }).InvokeAsync(
            context,
            new StubCatalog(null),
            new StubMembershipRepository(null),
            new TenantContextAccessor());

        Assert.True(called);
    }

    [Fact]
    public async Task PlatformFailureReturnsServiceUnavailable()
    {
        var context = AuthenticatedContext(TenantUid.ToString("D"));

        await Middleware(_ => Task.CompletedTask).InvokeAsync(
            context,
            new StubCatalog(exception: new InvalidOperationException()),
            new StubMembershipRepository(Membership()),
            new TenantContextAccessor());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    private static TenantResolutionMiddleware Middleware(RequestDelegate next) =>
        new(next, NullLogger<TenantResolutionMiddleware>.Instance);

    private static DefaultHttpContext AuthenticatedContext(string? tenantId)
    {
        var claims = new List<Claim> { new("sub", "identity-user-id") };
        if (tenantId is not null)
        {
            claims.Add(new Claim(MicroEmrClaimTypes.TenantId, tenantId));
        }

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
    }

    private static Tenant ActiveTenant() => Tenant(TenantStatus.Active);

    private static Tenant Tenant(TenantStatus status) =>
        new(
            TenantUid,
            "platform-key",
            "Platform Display Name",
            status,
            "America/Toronto",
            DateTimeOffset.UtcNow);

    private static UserTenantMembershipInfo Membership() =>
        new(
            "identity-user-id",
            TenantUid,
            "platform-key",
            "Platform Display Name",
            "Active",
            true,
            ["ClinicAdministrator"]);

    private sealed class StubCatalog(
        Tenant? tenant = null,
        Exception? exception = null) : ITenantCatalog
    {
        public Task<Tenant?> GetByUidAsync(
            Guid tenantUid,
            CancellationToken cancellationToken = default) =>
            exception is null
                ? Task.FromResult(tenant)
                : Task.FromException<Tenant?>(exception);

        public Task<Tenant?> GetByKeyAsync(
            string tenantKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(tenant);
    }

    private sealed class StubMembershipRepository(
        UserTenantMembershipInfo? membership) : IUserTenantMembershipRepository
    {
        public Task<IReadOnlyList<UserTenantMembershipInfo>> GetActiveMembershipsAsync(
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserTenantMembershipInfo>>(
                membership is null ? [] : [membership]);

        public Task<UserTenantMembershipInfo?> GetMembershipAsync(
            string userId,
            Guid tenantUid,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(membership);
    }

    private sealed class TestException : Exception;
}
