using MicroEMR.Api;
using MicroEMR.Application.Tenancy;
using MicroEMR.Infrastructure.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class DeferredTenantContextTests
{
    [Fact]
    public void ConstructionDoesNotRequireTenantButPropertyAccessDoes()
    {
        var context = new DeferredTenantContext(new TenantContextAccessor());

        Assert.Throws<InvalidOperationException>(() => context.TenantUid);
        Assert.Throws<InvalidOperationException>(() => context.TenantKey);
        Assert.Throws<InvalidOperationException>(() => context.DisplayName);
    }

    [Fact]
    public async Task TenantDatabaseConnectionFailsBeforeResolverOrSecretLookupWithoutTenant()
    {
        var resolver = new TrackingDatabaseResolver();
        var secrets = new TrackingSecretProvider();
        var factory = new TenantSqlConnectionFactory(
            new DeferredTenantContext(new TenantContextAccessor()),
            resolver,
            secrets,
            NullLogger<TenantSqlConnectionFactory>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.OpenConnectionAsync());
        Assert.Equal(0, resolver.Calls);
        Assert.Equal(0, secrets.Calls);
    }

    [Fact]
    public void PropertiesFollowTheResolvedTenant()
    {
        var accessor = new TenantContextAccessor();
        var expected = new TenantContext(Guid.NewGuid(), "clinic-a", "Clinic A");
        var context = new DeferredTenantContext(accessor);

        accessor.SetTenant(expected);

        Assert.Equal(expected.TenantUid, context.TenantUid);
        Assert.Equal(expected.TenantKey, context.TenantKey);
        Assert.Equal(expected.DisplayName, context.DisplayName);
    }

    private sealed class TrackingDatabaseResolver : ITenantDatabaseResolver
    {
        public int Calls { get; private set; }

        public Task<TenantDatabaseInfo?> ResolveAsync(
            Guid tenantUid,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<TenantDatabaseInfo?>(null);
        }
    }

    private sealed class TrackingSecretProvider : ITenantDatabaseSecretProvider
    {
        public int Calls { get; private set; }

        public Task<TenantDatabaseSecret> ResolveAsync(
            string secretReference,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new TenantDatabaseSecret(string.Empty));
        }
    }
}
