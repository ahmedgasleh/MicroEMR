using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Auth.Data;
using MicroEMR.Auth.Extensions;
using MicroEMR.Auth.Services.Tenancy;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using MicroEMR.Application.Security;
using MicroEMR.Auth.Services.PlatformEntitlements;
using System.Globalization;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MicroEMR.Auth.Controllers;

public sealed class AuthorizationController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly ITenantClaimEnricher _tenantClaimEnricher;
    private readonly IUserTenantResolver _tenantResolver;
    private readonly IUserTenantMembershipService _membershipService;
    private readonly IPendingTenantSelectionStore _selectionStore;
    private readonly ILogger<AuthorizationController> _logger;
    private readonly IPlatformEntitlementClaimService _platformEntitlements;
    private readonly IPlatformRefreshAuthorizationService _platformRefreshAuthorization;

    public AuthorizationController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictScopeManager scopeManager,
        ITenantClaimEnricher tenantClaimEnricher,
        IUserTenantResolver tenantResolver,
        IUserTenantMembershipService membershipService,
        IPendingTenantSelectionStore selectionStore,
        IPlatformEntitlementClaimService platformEntitlements,
        IPlatformRefreshAuthorizationService platformRefreshAuthorization,
        ILogger<AuthorizationController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _scopeManager = scopeManager;
        _tenantClaimEnricher = tenantClaimEnricher;
        _tenantResolver = tenantResolver;
        _membershipService = membershipService;
        _selectionStore = selectionStore;
        _platformEntitlements = platformEntitlements;
        _platformRefreshAuthorization = platformRefreshAuthorization;
        _logger = logger;
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request =
            HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException(
                "The OpenID Connect request cannot be retrieved.");

        var authenticationResult =
            await HttpContext.AuthenticateAsync(
                IdentityConstants.ApplicationScheme);

        if (!authenticationResult.Succeeded ||
            authenticationResult.Principal is null)
        {
            var returnUrl =
                Request.PathBase +
                Request.Path +
                QueryString.Create(
                    Request.HasFormContentType
                        ? Request.Form
                            .Where(parameter =>
                                parameter.Key !=
                                Parameters.ClientSecret)
                            .ToList()
                        : Request.Query.ToList());

            return Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = returnUrl
                },
                IdentityConstants.ApplicationScheme);
        }

        var identityUser =
            await _userManager.GetUserAsync(
                authenticationResult.Principal);

        if (!await IsEligibleAsync(identityUser))
        {
            return Forbid(
                OpenIddictServerAspNetCoreDefaults
                    .AuthenticationScheme);
        }

        var identity = new ClaimsIdentity(
            authenticationType:
                OpenIddictServerAspNetCoreDefaults
                    .AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(
            Claims.Subject,
            await _userManager.GetUserIdAsync(identityUser!));

        identity.SetClaim(
            Claims.Name,
            identityUser!.FullName
            ?? identityUser.UserName
            ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(identityUser.Email))
        {
            identity.SetClaim(
                Claims.Email,
                identityUser.Email);
        }

        var roles =
            await _userManager.GetRolesAsync(identityUser);

        foreach (var role in roles)
        {
            identity.AddClaim(
                new Claim(Claims.Role, role));
        }

        TenantClaimEnrichmentResult tenantResult;
        var continuationId = Request.Query["tenant_continuation"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(continuationId))
        {
            var continuation = await _selectionStore.TakeContinuationAsync(
                continuationId, HttpContext.RequestAborted);
            var resumedReturnUrl = Request.PathBase + Request.Path + QueryString.Create(
                Request.Query.Where(parameter => parameter.Key != "tenant_continuation").ToList());
            if (continuation is null || continuation.ExpiresAt <= DateTimeOffset.UtcNow ||
                !string.Equals(continuation.UserId, identityUser.Id, StringComparison.Ordinal) ||
                !string.Equals(continuation.ReturnUrl, resumedReturnUrl, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Tenant selection continuation rejected for user {UserId}. TraceIdentifier: {TraceIdentifier}",
                    identityUser.Id, HttpContext.TraceIdentifier);
                return TenantForbid("Your clinic selection has expired. Please sign in again.");
            }

            var memberships = await _membershipService.GetActiveMembershipsAsync(
                identityUser, HttpContext.RequestAborted);
            var selected = memberships.SingleOrDefault(m => m.TenantUid == continuation.SelectedTenantUid);
            if (selected is null)
            {
                _logger.LogWarning(
                    "Selected tenant membership was revoked before authorization completion for user {UserId}. TraceIdentifier: {TraceIdentifier}",
                    identityUser.Id, HttpContext.TraceIdentifier);
                return TenantForbid("You no longer have access to the selected clinic.");
            }

            tenantResult = _tenantClaimEnricher.EnrichFromValidatedMembership(identity, selected);
            _logger.LogInformation(
                "Authorization resumed successfully for user {UserId}, tenant {TenantUid}. TraceIdentifier: {TraceIdentifier}",
                identityUser.Id, selected.TenantUid, HttpContext.TraceIdentifier);
        }
        else
        {
            TenantMembershipResolutionResult resolution;
            try
            {
                resolution = await _tenantResolver.ResolveAsync(identityUser, HttpContext.RequestAborted);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception,
                    "Invalid tenant membership state for user {UserId}. TraceIdentifier: {TraceIdentifier}",
                    identityUser.Id, HttpContext.TraceIdentifier);
                return TenantForbid("Your account could not be assigned to a clinic.");
            }

            if (resolution.Status == TenantMembershipResolutionStatus.SelectionRequired)
            {
                _logger.LogInformation(
                    "Tenant selection required for user {UserId}. TraceIdentifier: {TraceIdentifier}",
                    identityUser.Id, HttpContext.TraceIdentifier);
                var returnUrl = Request.PathBase + Request.Path + Request.QueryString;
                if (!Url.IsLocalUrl(returnUrl))
                {
                    return TenantForbid("The authorization request could not be continued.");
                }

                var selectionId = WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
                var now = DateTimeOffset.UtcNow;
                await _selectionStore.StoreAsync(new PendingTenantSelection(
                    selectionId, identityUser.Id, returnUrl, now, now.AddMinutes(5),
                    resolution.AvailableMemberships.Select(m => m.TenantUid).Distinct().ToArray()),
                    HttpContext.RequestAborted);
                _logger.LogInformation(
                    "Pending tenant selection created for user {UserId}, selection {SelectionId}. TraceIdentifier: {TraceIdentifier}",
                    identityUser.Id, selectionId, HttpContext.TraceIdentifier);
                return RedirectToAction("SelectTenant", "Account", new { selectionId });
            }

            tenantResult = await _tenantClaimEnricher.EnrichAsync(
                identityUser, identity, HttpContext.TraceIdentifier, HttpContext.RequestAborted);
        }

        if (tenantResult.Status != TenantClaimEnrichmentStatus.Resolved)
        {
            return TenantForbid(tenantResult.ErrorDescription ?? "Your account could not be assigned to a clinic.");
        }

        PlatformAuthorizationSnapshot platformAuthorization;
        try
        {
            platformAuthorization = await _platformEntitlements.LoadAsync(
                identityUser!.Id,
                HttpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Platform authorization state could not be loaded for token issuance. TraceIdentifier: {TraceIdentifier}",
                HttpContext.TraceIdentifier);
            return TokenForbid(Errors.ServerError, "The authorization service is temporarily unavailable.");
        }

        AddPlatformAuthorizationClaims(identity, platformAuthorization);

        var principal =
            new ClaimsPrincipal(identity);

        // Only grant scopes that were actually requested.
        principal.SetScopes(
            request.GetScopes());

        // Resolve resources attached to those scopes.
        var resources = new List<string>();

        await foreach (var resource in
            _scopeManager.ListResourcesAsync(
                principal.GetScopes()))
        {
            resources.Add(resource);
        }

        principal.SetResources(resources);

        principal.SetDestinations();

        return SignIn(
            principal,
            OpenIddictServerAspNetCoreDefaults
                .AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");
        var authentication = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = authentication.Principal;
        _logger.LogInformation(
            "OpenID Connect token endpoint processing grant type {GrantType}. TraceIdentifier: {TraceIdentifier}",
            request.GrantType ?? "missing",
            HttpContext.TraceIdentifier);

        if (!request.IsRefreshTokenGrantType())
        {
            // Only authorization-code and refresh grants are enabled. If OpenIddict passes a
            // non-refresh request through with a trusted principal, it has already validated the
            // code, client and PKCE verifier. Return that principal for normal token issuance.
            return authentication.Succeeded && principal is not null
                ? SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
                : TokenForbid(Errors.InvalidGrant, "The authorization grant is no longer valid.");
        }

        var identityUserId = principal?.GetClaim(Claims.Subject);
        if (!authentication.Succeeded || principal is null || string.IsNullOrWhiteSpace(identityUserId))
        {
            return TokenForbid(Errors.InvalidGrant, "The refresh token is no longer valid.");
        }

        var identityUser = await _userManager.FindByIdAsync(identityUserId);
        if (!await IsEligibleAsync(identityUser))
        {
            return TokenForbid(Errors.InvalidGrant, "The refresh token is no longer valid.");
        }

        var trustedVersionValue = principal.GetClaim(MicroEmrClaimTypes.PlatformAuthorizationVersion);
        if (!long.TryParse(
                trustedVersionValue,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var trustedVersion))
        {
            return TokenForbid(Errors.InvalidGrant, "The refresh token is no longer valid.");
        }

        try
        {
            var currentAuthorization = await _platformRefreshAuthorization.ValidateAndLoadAsync(
                identityUserId,
                trustedVersion,
                HttpContext.RequestAborted);
            if (currentAuthorization is null)
            {
                _logger.LogInformation(
                    "A stale platform authorization refresh was rejected for user {UserId}. TraceIdentifier: {TraceIdentifier}",
                    identityUserId,
                    HttpContext.TraceIdentifier);
                return TokenForbid(Errors.InvalidGrant, "The refresh token is no longer valid.");
            }

            var identity = principal.Identity as ClaimsIdentity
                ?? throw new InvalidOperationException("The refresh principal has no claims identity.");
            foreach (var claim in identity.FindAll(MicroEmrClaimTypes.PlatformEntitlement).ToArray())
            {
                identity.RemoveClaim(claim);
            }
            foreach (var claim in identity.FindAll(MicroEmrClaimTypes.PlatformAuthorizationVersion).ToArray())
            {
                identity.RemoveClaim(claim);
            }

            AddPlatformAuthorizationClaims(identity, currentAuthorization);
            principal.SetDestinations();
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Platform authorization state could not be validated during refresh. TraceIdentifier: {TraceIdentifier}",
                HttpContext.TraceIdentifier);
            return TokenForbid(Errors.ServerError, "The authorization service is temporarily unavailable.");
        }
    }

    private async Task<bool> IsEligibleAsync(ApplicationUser? user)
    {
        if (user is null || !user.IsActive)
        {
            return false;
        }

        return !_userManager.SupportsUserLockout || !await _userManager.IsLockedOutAsync(user);
    }

    private static void AddPlatformAuthorizationClaims(
        ClaimsIdentity identity,
        PlatformAuthorizationSnapshot snapshot)
    {
        foreach (var entitlement in snapshot.Entitlements)
        {
            identity.AddClaim(new Claim(MicroEmrClaimTypes.PlatformEntitlement, entitlement));
        }

        identity.AddClaim(new Claim(
            MicroEmrClaimTypes.PlatformAuthorizationVersion,
            snapshot.AuthorizationVersion.ToString(CultureInfo.InvariantCulture)));
    }

    private IActionResult TokenForbid(string error, string description) => Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
        }), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

    private IActionResult TenantForbid(string description) => Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
        }), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        return SignOut(
            OpenIddictServerAspNetCoreDefaults
                .AuthenticationScheme);
    }

}
