using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MicroEMR.Application.PlatformEntitlements;
using MicroEMR.Application.Security;
using Xunit;
using ApiHandler = MicroEMR.Api.Authorization.PlatformEntitlementAuthorizationHandler;
using ApiRequirement = MicroEMR.Api.Authorization.PlatformEntitlementRequirement;
using WebHandler = MicroEMR.Web.Authorization.PlatformEntitlementAuthorizationHandler;
using WebRequirement = MicroEMR.Web.Authorization.PlatformEntitlementRequirement;

namespace MicroEMR.Api.Tests;

public sealed class PlatformEntitlementAuthorizationTests
{
    [Fact]
    public async Task ExactPlatformEntitlementAuthorizesInApiAndWebWithoutTenantOrClinicalClaims()
    {
        var user = Principal(new Claim(
            MicroEmrClaimTypes.PlatformEntitlement,
            PlatformEntitlementKeys.SecurityAuditView));

        Assert.True(await ApiAuthorized(user, PlatformEntitlementKeys.SecurityAuditView));
        Assert.True(await WebAuthorized(user, PlatformEntitlementKeys.SecurityAuditView));
        Assert.DoesNotContain(user.Claims, claim =>
            claim.Type == MicroEmrClaimTypes.TenantId || claim.Type == "clinical_user_id");
    }

    [Theory]
    [InlineData("PlatformAdministrator")]
    [InlineData("PlatformOperator")]
    [InlineData("Administrator")]
    [InlineData("SystemAdmin")]
    [InlineData("Physician")]
    [InlineData("Nurse")]
    [InlineData("MedicalAssistant")]
    public async Task RoleAloneNeverSatisfiesPlatformEntitlement(string role)
    {
        var user = Principal(new Claim(ClaimTypes.Role, role));

        Assert.False(await ApiAuthorized(user, PlatformEntitlementKeys.SecurityAuditView));
        Assert.False(await WebAuthorized(user, PlatformEntitlementKeys.SecurityAuditView));
    }

    [Fact]
    public async Task TenantPermissionTextCannotSatisfyPlatformEntitlement()
    {
        var user = Principal(new Claim("permission", PlatformEntitlementKeys.SecurityAuditView));

        Assert.False(await ApiAuthorized(user, PlatformEntitlementKeys.SecurityAuditView));
        Assert.False(await WebAuthorized(user, PlatformEntitlementKeys.SecurityAuditView));
    }

    [Fact]
    public async Task WrongOrAbsentEntitlementFailsClosed()
    {
        Assert.False(await ApiAuthorized(Principal(), PlatformEntitlementKeys.SecurityAuditView));
        Assert.False(await WebAuthorized(Principal(), PlatformEntitlementKeys.SecurityAuditView));
        Assert.False(await ApiAuthorized(
            Principal(new Claim(MicroEmrClaimTypes.PlatformEntitlement, "SecurityAudit.Export")),
            PlatformEntitlementKeys.SecurityAuditView));
    }

    [Fact]
    public async Task UnknownRequirementFailsClosedEvenWithMatchingClaim()
    {
        var user = Principal(new Claim(MicroEmrClaimTypes.PlatformEntitlement, "Unknown.Value"));

        Assert.False(await ApiAuthorized(user, "Unknown.Value"));
        Assert.False(await WebAuthorized(user, "Unknown.Value"));
        Assert.Throws<ArgumentException>(() => PlatformEntitlementPolicies.For("Unknown.Value"));
    }

    [Fact]
    public void CanonicalPolicyNameIsStable()
    {
        Assert.Equal(
            "PlatformEntitlement:SecurityAudit.View",
            PlatformEntitlementPolicies.SecurityAuditView);
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    private static async Task<bool> ApiAuthorized(ClaimsPrincipal user, string key)
    {
        var requirement = new ApiRequirement(key);
        var context = new AuthorizationHandlerContext([requirement], user, null);
        await new ApiHandler().HandleAsync(context);
        return context.HasSucceeded;
    }

    private static async Task<bool> WebAuthorized(ClaimsPrincipal user, string key)
    {
        var requirement = new WebRequirement(key);
        var context = new AuthorizationHandlerContext([requirement], user, null);
        await new WebHandler(new StubWebEntitlementAccessor()).HandleAsync(context);
        return context.HasSucceeded;
    }

    private sealed class StubWebEntitlementAccessor : MicroEMR.Web.Authorization.IWebPlatformEntitlementAccessor
    {
        public Task<bool> HasAsync(string entitlementKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
