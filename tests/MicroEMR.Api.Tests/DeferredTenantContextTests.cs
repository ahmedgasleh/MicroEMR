using MicroEMR.Api;
using MicroEMR.Application.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class DeferredTenantContextTests
{
    [Fact]
    public void ConstructionDoesNotRequireTenantButPropertyAccessDoes()
    {
        var context = new DeferredTenantContext(new TenantContextAccessor());

        Assert.Throws<InvalidOperationException>(() => context.TenantUid);
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
}
