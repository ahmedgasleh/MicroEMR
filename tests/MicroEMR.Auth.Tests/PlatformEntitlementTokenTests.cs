using System.Security.Claims;
using MicroEMR.Application.PlatformEntitlements;
using MicroEMR.Application.Security;
using MicroEMR.Auth.Extensions;
using MicroEMR.Auth.Services.PlatformEntitlements;
using OpenIddict.Abstractions;
using Xunit;

namespace MicroEMR.Auth.Tests;

public sealed class PlatformEntitlementTokenTests
{
    [Fact]
    public void TokenLifetimesAreExplicitAndTokenEndpointUsesPassthrough()
    {
        var source = ReadSource("src", "MicroEMR.Auth", "Program.cs");

        Assert.Contains("SetAccessTokenLifetime(TimeSpan.FromMinutes(5))", source);
        Assert.Contains("SetRefreshTokenLifetime(TimeSpan.FromDays(14))", source);
        Assert.Contains("EnableTokenEndpointPassthrough()", source);
    }

    [Fact]
    public async Task ClaimLoaderReturnsOnlyKnownDistinctCurrentEntitlementsAndVersion()
    {
        var repository = new StubEntitlementService(
            [PlatformEntitlementKeys.SecurityAuditView, PlatformEntitlementKeys.SecurityAuditView, "Unknown.Value"],
            7);

        var snapshot = await new PlatformEntitlementClaimService(repository).LoadAsync("identity-user");

        Assert.Equal([PlatformEntitlementKeys.SecurityAuditView], snapshot.Entitlements);
        Assert.Equal(7, snapshot.AuthorizationVersion);
        Assert.Equal("identity-user", repository.LastUserId);
    }

    [Fact]
    public async Task ClaimLoaderWithoutAssignmentProducesNoEntitlementClaimValues()
    {
        var snapshot = await new PlatformEntitlementClaimService(
            new StubEntitlementService([], 0)).LoadAsync("identity-user");

        Assert.Empty(snapshot.Entitlements);
        Assert.Equal(0, snapshot.AuthorizationVersion);
    }

    [Fact]
    public async Task RefreshWithUnchangedVersionReloadsAndReturnsCurrentEntitlements()
    {
        var loader = new SequenceClaimService(
            new([PlatformEntitlementKeys.SecurityAuditView], 4),
            new([PlatformEntitlementKeys.SecurityAuditView], 4));

        var result = await new PlatformRefreshAuthorizationService(loader)
            .ValidateAndLoadAsync("identity-user", 4);

        Assert.NotNull(result);
        Assert.Equal([PlatformEntitlementKeys.SecurityAuditView], result!.Entitlements);
        Assert.Equal(2, loader.CallCount);
    }

    [Fact]
    public async Task RefreshRejectsStaleTrustedVersionBeforeMinting()
    {
        var loader = new SequenceClaimService(new PlatformAuthorizationSnapshot([], 9));

        var result = await new PlatformRefreshAuthorizationService(loader)
            .ValidateAndLoadAsync("identity-user", 8);

        Assert.Null(result);
        Assert.Equal(1, loader.CallCount);
    }

    [Fact]
    public async Task RefreshRejectsAuthorizationChangeDuringEntitlementReload()
    {
        var loader = new SequenceClaimService(
            new([PlatformEntitlementKeys.SecurityAuditView], 11),
            new([], 12));

        var result = await new PlatformRefreshAuthorizationService(loader)
            .ValidateAndLoadAsync("identity-user", 11);

        Assert.Null(result);
        Assert.Equal(2, loader.CallCount);
    }

    [Fact]
    public void PlatformClaimsAreAccessTokenOnlyEvenWhenOpenIdIsRequested()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
        principal.SetScopes(OpenIddictConstants.Scopes.OpenId);
        var entitlement = new Claim(
            MicroEmrClaimTypes.PlatformEntitlement,
            PlatformEntitlementKeys.SecurityAuditView);
        var version = new Claim(MicroEmrClaimTypes.PlatformAuthorizationVersion, "3");

        Assert.Equal(
            [OpenIddictConstants.Destinations.AccessToken],
            ClaimsPrincipalExtensions.GetDestinations(entitlement, principal));
        Assert.Equal(
            [OpenIddictConstants.Destinations.AccessToken],
            ClaimsPrincipalExtensions.GetDestinations(version, principal));
    }

    [Fact]
    public void RefreshVersionComesFromValidatedPrincipalAndNotRequestParameter()
    {
        var source = ReadSource("src", "MicroEMR.Auth", "Controllers", "AuthorizationController.cs");

        Assert.Contains("principal.GetClaim(MicroEmrClaimTypes.PlatformAuthorizationVersion)", source);
        Assert.DoesNotContain("request.GetParameter(MicroEmrClaimTypes.PlatformAuthorizationVersion)", source);
        Assert.Contains("Errors.InvalidGrant", source);
    }

    [Fact]
    public void TokenEndpointPreservesAuthorizationCodeAndPkceExchange()
    {
        var source = ReadSource("src", "MicroEMR.Auth", "Controllers", "AuthorizationController.cs");
        Assert.Contains("if (!request.IsRefreshTokenGrantType())", source);
        Assert.Contains("authentication.Succeeded && principal is not null", source);
        Assert.Contains(
            "SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)",
            source);
        Assert.DoesNotContain("Errors.UnsupportedGrantType", source);
    }

    private static string ReadSource(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MicroEMR.slnx")))
            directory = directory.Parent;
        return File.ReadAllText(Path.Combine(directory!.FullName, Path.Combine(parts)));
    }

    private sealed class SequenceClaimService(params PlatformAuthorizationSnapshot[] values)
        : IPlatformEntitlementClaimService
    {
        public int CallCount { get; private set; }

        public Task<PlatformAuthorizationSnapshot> LoadAsync(
            string identityUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(values[CallCount++]);
    }

    private sealed class StubEntitlementService(IReadOnlyList<string> values, long version)
        : IPlatformEntitlementService
    {
        public string? LastUserId { get; private set; }

        public Task<IReadOnlyList<string>> GetActiveForUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            return Task.FromResult(values);
        }

        public Task<long> GetAuthorizationVersionAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(version);

        public Task<PlatformEntitlementChangeResult> AssignAsync(string userId, string entitlementKey, string actorUserId, Guid correlationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PlatformEntitlementChangeResult> RevokeAsync(string userId, string entitlementKey, string actorUserId, Guid correlationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
