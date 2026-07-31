using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Controllers;
using MicroEMR.Application.Tenancy;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TenantContextControllerTests
{
    [Fact]
    public void ResponseContainsOnlySafeTenantContextValues()
    {
        var tenantUid = Guid.NewGuid();
        var controller = new TenantContextController(
            new TenantContext(tenantUid, "local-dev", "Local Clinic"));

        var result = Assert.IsType<OkObjectResult>(controller.Get());
        var json = JsonSerializer.Serialize(result.Value);

        Assert.Contains("tenantUid", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tenantKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("displayName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("database", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("server", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection", json, StringComparison.OrdinalIgnoreCase);
    }
}
