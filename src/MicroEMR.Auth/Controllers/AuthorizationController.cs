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

    public AuthorizationController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictScopeManager scopeManager,
        ITenantClaimEnricher tenantClaimEnricher,
        IUserTenantResolver tenantResolver,
        IUserTenantMembershipService membershipService,
        IPendingTenantSelectionStore selectionStore,
        ILogger<AuthorizationController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _scopeManager = scopeManager;
        _tenantClaimEnricher = tenantClaimEnricher;
        _tenantResolver = tenantResolver;
        _membershipService = membershipService;
        _selectionStore = selectionStore;
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

        if (identityUser is null ||
            !identityUser.IsActive)
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
            await _userManager.GetUserIdAsync(identityUser));

        identity.SetClaim(
            Claims.Name,
            identityUser.FullName
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
