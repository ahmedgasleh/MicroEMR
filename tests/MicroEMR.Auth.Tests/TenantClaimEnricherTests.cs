using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using MicroEMR.Application.Security;
using MicroEMR.Application.Tenancy;
using MicroEMR.Auth.Data;
using MicroEMR.Auth.Extensions;
using MicroEMR.Auth.Services.Tenancy;
using OpenIddict.Abstractions;
using Xunit;

namespace MicroEMR.Auth.Tests;

public sealed class TenantClaimEnricherTests
{
    private static readonly Guid TenantUid =
        Guid.Parse("e9544dad-9a61-4630-9b2b-dd9b58ecdf43");

    [Fact]
    public async Task ResolvedMembership_ReplacesTenantClaims_AndPreservesGlobalRoles()
    {
        var membership = Membership(
            ["Physician", "ClinicAdministrator", "Physician", ""]);
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, "GlobalAdministrator"));
        identity.AddClaim(new Claim(MicroEmrClaimTypes.TenantId, Guid.Empty.ToString()));
        identity.AddClaim(new Claim(MicroEmrClaimTypes.TenantRole, "StaleRole"));

        var result = await CreateEnricher(Resolved(membership))
            .EnrichAsync(User(), identity, "trace-id");

        Assert.Equal(TenantClaimEnrichmentStatus.Resolved, result.Status);
        Assert.Equal(TenantUid.ToString("D"), identity.FindFirst(MicroEmrClaimTypes.TenantId)?.Value);
        Assert.Equal("local-dev", identity.FindFirst(MicroEmrClaimTypes.TenantKey)?.Value);
        Assert.Equal("Local Development Clinic", identity.FindFirst(MicroEmrClaimTypes.TenantName)?.Value);
        Assert.Equal(
            ["ClinicAdministrator", "Physician"],
            identity.FindAll(MicroEmrClaimTypes.TenantRole).Select(claim => claim.Value));
        Assert.Equal("GlobalAdministrator", identity.FindFirst(OpenIddictConstants.Claims.Role)?.Value);
        Assert.DoesNotContain(identity.Claims, claim => claim.Value == "StaleRole");
        Assert.DoesNotContain(identity.Claims, claim =>
            claim.Type.Contains("Database", StringComparison.OrdinalIgnoreCase) ||
            claim.Type.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
            claim.Type.Contains("Connection", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(TenantMembershipResolutionStatus.None, TenantClaimEnrichmentStatus.NoActiveMembership)]
    [InlineData(TenantMembershipResolutionStatus.SelectionRequired, TenantClaimEnrichmentStatus.SelectionRequired)]
    public async Task UnresolvedMembership_DoesNotAddTenantClaims(
        TenantMembershipResolutionStatus resolutionStatus,
        TenantClaimEnrichmentStatus expectedStatus)
    {
        var resolution = new TenantMembershipResolutionResult(
            resolutionStatus,
            null,
            Array.Empty<UserTenantMembershipInfo>());
        var identity = new ClaimsIdentity();

        var result = await CreateEnricher(resolution)
            .EnrichAsync(User(), identity, "trace-id");

        Assert.Equal(expectedStatus, result.Status);
        Assert.Empty(identity.FindAll(MicroEmrClaimTypes.TenantId));
    }

    [Fact]
    public async Task InvalidMembershipData_DoesNotAddTenantClaims()
    {
        var identity = new ClaimsIdentity();
        var enricher = new TenantClaimEnricher(
            new ThrowingResolver(),
            NullLogger<TenantClaimEnricher>.Instance);

        var result = await enricher.EnrichAsync(User(), identity, "trace-id");

        Assert.Equal(TenantClaimEnrichmentStatus.InvalidMembershipData, result.Status);
        Assert.Empty(identity.FindAll(MicroEmrClaimTypes.TenantId));
    }

    [Fact]
    public async Task ResolutionFailure_DoesNotEscapeOrAddTenantClaims()
    {
        var identity = new ClaimsIdentity();
        var enricher = new TenantClaimEnricher(
            new ThrowingResolver(new InvalidOperationException("Platform unavailable.")),
            NullLogger<TenantClaimEnricher>.Instance);

        var result = await enricher.EnrichAsync(User(), identity, "trace-id");

        Assert.Equal(TenantClaimEnrichmentStatus.InvalidMembershipData, result.Status);
        Assert.Empty(identity.FindAll(MicroEmrClaimTypes.TenantId));
    }

    [Fact]
    public async Task Cancellation_IsNotConvertedToAccessDenied()
    {
        var enricher = new TenantClaimEnricher(
            new ThrowingResolver(new OperationCanceledException()),
            NullLogger<TenantClaimEnricher>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            enricher.EnrichAsync(User(), new ClaimsIdentity(), "trace-id"));
    }

    [Fact]
    public void TenantClaimDestinations_AreExplicit()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        principal.SetScopes(OpenIddictConstants.Scopes.OpenId);

        Assert.Equal(
            [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            ClaimsPrincipalExtensions.GetDestinations(
                new Claim(MicroEmrClaimTypes.TenantId, TenantUid.ToString("D")),
                principal));
        Assert.Equal(
            [OpenIddictConstants.Destinations.AccessToken],
            ClaimsPrincipalExtensions.GetDestinations(
                new Claim(MicroEmrClaimTypes.TenantRole, "Physician"),
                principal));
    }

    private static TenantClaimEnricher CreateEnricher(
        TenantMembershipResolutionResult result) =>
        new(new StubResolver(result), NullLogger<TenantClaimEnricher>.Instance);

    private static ApplicationUser User() => new() { Id = "identity-user-id" };

    private static UserTenantMembershipInfo Membership(IReadOnlyCollection<string> roles) =>
        new(
            "identity-user-id",
            TenantUid,
            "local-dev",
            "Local Development Clinic",
            "Active",
            true,
            roles);

    private static TenantMembershipResolutionResult Resolved(
        UserTenantMembershipInfo membership) =>
        new(TenantMembershipResolutionStatus.Resolved, membership, [membership]);

    private sealed class StubResolver(TenantMembershipResolutionResult result)
        : IUserTenantResolver
    {
        public Task<TenantMembershipResolutionResult> ResolveAsync(
            ApplicationUser user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingResolver(Exception? exception = null) : IUserTenantResolver
    {
        public Task<TenantMembershipResolutionResult> ResolveAsync(
            ApplicationUser user,
            CancellationToken cancellationToken = default) =>
            throw exception ??
                new InvalidTenantMembershipDataException(user.Id, "Invalid test data.");
    }
}
