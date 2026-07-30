using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.Security;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class TenantRoleAuthorizationHandlerTests
{
    [Fact]
    public async Task TenantRoleClaimSatisfiesRequirement()
    {
        var requirement = new TenantRoleRequirement("ClinicAdministrator");
        var user = Principal(new Claim(
            MicroEmrClaimTypes.TenantRole,
            "ClinicAdministrator"));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await new TenantRoleAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task GlobalRoleClaimDoesNotSatisfyTenantRequirement()
    {
        var requirement = new TenantRoleRequirement("ClinicAdministrator");
        var user = Principal(new Claim(
            ClaimTypes.Role,
            "ClinicAdministrator"));
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await new TenantRoleAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static ClaimsPrincipal Principal(Claim claim) =>
        new(new ClaimsIdentity([claim], "test"));
}
