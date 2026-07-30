using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Application.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TenantContextAccessorTests
{
    [Fact]
    public void DifferentTenantCannotReplaceCurrentTenant()
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(Context(Guid.NewGuid()));

        Assert.Throws<InvalidOperationException>(() =>
            accessor.SetTenant(Context(Guid.NewGuid())));
    }

    [Fact]
    public void RequiredTenantResolutionFailsWithoutCurrentContext()
    {
        var services = new ServiceCollection();
        services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
        services.AddScoped<ITenantContext>(provider =>
            provider.GetRequiredService<ITenantContextAccessor>().Current
            ?? throw new InvalidOperationException(
                "Tenant context has not been established for the current operation."));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<ITenantContext>());
        Assert.Contains("has not been established", exception.Message);
    }

    private static TenantContext Context(Guid tenantUid) =>
        new(tenantUid, "key", "Display");
}
