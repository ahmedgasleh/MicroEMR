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

    [Theory]
    [InlineData("Scheduler")]
    [InlineData("MedicalAssistant")]
    [InlineData("Nurse")]
    [InlineData("ClinicAdministrator")]
    public async Task AnyTenantRoleRequirement_AcceptsConfiguredTenantRoles(string role)
    {
        var requirement = new AnyTenantRoleRequirement(
            "Scheduler", "MedicalAssistant", "Nurse", "ClinicAdministrator");
        var context = new AuthorizationHandlerContext([requirement],
            Principal(new Claim(MicroEmrClaimTypes.TenantRole, role)), null);

        await new AnyTenantRoleAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task AnyTenantRoleRequirement_DoesNotAcceptGlobalPlatformRole()
    {
        var requirement = new AnyTenantRoleRequirement("ClinicAdministrator");
        var context = new AuthorizationHandlerContext([requirement],
            Principal(new Claim(ClaimTypes.Role, "PlatformAdministrator")), null);

        await new AnyTenantRoleAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static ClaimsPrincipal Principal(Claim claim) =>
        new(new ClaimsIdentity([claim], "test"));
}
