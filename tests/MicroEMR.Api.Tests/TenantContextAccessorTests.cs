using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Application.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TenantContextAccessorTests
{
    private static readonly Guid TenantUid = Guid.NewGuid();

    [Fact]
    public void ContextStartsEmptyAndCanBeCleared()
    {
        var accessor = new TenantContextAccessor();
        Assert.Null(accessor.Current);

        accessor.SetTenant(Context());
        Assert.Equal(TenantUid, accessor.Current!.TenantUid);

        accessor.Clear();
        Assert.Null(accessor.Current);
    }

    [Fact]
    public void ExactSameTenantReassignmentIsHarmless()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(Context());
        accessor.SetTenant(Context());

        Assert.Equal(TenantUid, accessor.Current!.TenantUid);
    }

    [Fact]
    public void ConflictingTenantReassignmentIsRejected()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(Context());

        Assert.Throws<InvalidOperationException>(() =>
            accessor.SetTenant(new TenantContext(Guid.NewGuid(), "other", "Other")));
        Assert.Throws<InvalidOperationException>(() =>
            accessor.SetTenant(new TenantContext(TenantUid, "conflict", "Tenant")));
    }

    [Fact]
    public async Task SeparateParallelScopesRemainIsolated()
    {
        var services = new ServiceCollection()
            .AddScoped<ITenantContextAccessor, TenantContextAccessor>()
            .BuildServiceProvider();
        var firstUid = Guid.NewGuid();
        var secondUid = Guid.NewGuid();

        var results = await Task.WhenAll(
            ResolveInScopeAsync(services, firstUid),
            ResolveInScopeAsync(services, secondUid));

        Assert.Equal([firstUid, secondUid], results);
    }

    private static async Task<Guid> ResolveInScopeAsync(
        IServiceProvider services,
        Guid tenantUid)
    {
        await Task.Yield();
        await using var scope = services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(new TenantContext(tenantUid, tenantUid.ToString("N"), "Tenant"));
        return accessor.Current!.TenantUid;
    }

    private static TenantContext Context() =>
        new(TenantUid, "tenant", "Tenant");
}
