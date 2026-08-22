using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Auth.Data;
using MicroEMR.Auth.Models;
using MicroEMR.Auth.Services.Tenancy;
using MicroEMR.Auth.Services.SecurityAudit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.WebUtilities;

namespace MicroEMR.Auth.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserTenantMembershipService _membershipService;
    private readonly IPendingTenantSelectionStore _selectionStore;
    private readonly TenantSelectionSecurityAuditRecorder _securityAudit;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IUserTenantMembershipService membershipService,
        IPendingTenantSelectionStore selectionStore,
        TenantSelectionSecurityAuditRecorder securityAudit,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _membershipService = membershipService;
        _selectionStore = selectionStore;
        _securityAudit = securityAudit;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Redirect("/");
        }

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result =
            await _signInManager.PasswordSignInAsync(
                model.Username,
                model.Password,
                isPersistent: true,
                lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                "Invalid username or password.");

            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) &&
            Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return Redirect("/");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> SelectTenant(string? selectionId)
    {
        var user = await _userManager.GetUserAsync(User);
        var pending = string.IsNullOrWhiteSpace(selectionId)
            ? null
            : await _selectionStore.GetAsync(selectionId, HttpContext.RequestAborted);
        if (user is null || pending is null || pending.ExpiresAt <= DateTimeOffset.UtcNow ||
            !string.Equals(pending.UserId, user.Id, StringComparison.Ordinal))
        {
            _logger.LogWarning("Tenant selection GET rejected. SelectionId: {SelectionId}. TraceIdentifier: {TraceIdentifier}",
                selectionId, HttpContext.TraceIdentifier);
            if (pending?.ExpiresAt <= DateTimeOffset.UtcNow)
                await _selectionStore.RemoveAsync(selectionId!, HttpContext.RequestAborted);
            return View("TenantSelectionExpired");
        }

        var memberships = await _membershipService.GetActiveMembershipsAsync(user, HttpContext.RequestAborted);
        var options = memberships
            .Where(m => pending.AllowedTenantUids.Contains(m.TenantUid))
            .Select(m => new TenantSelectionOptionViewModel(m.TenantUid, m.TenantKey, m.TenantDisplayName))
            .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _logger.LogInformation("Tenant selection page opened for user {UserId}, selection {SelectionId}. TraceIdentifier: {TraceIdentifier}",
            user.Id, pending.SelectionId, HttpContext.TraceIdentifier);
        return View(new TenantSelectionViewModel { SelectionId = pending.SelectionId, Tenants = options });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectTenant(TenantSelectionViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        var pending = string.IsNullOrWhiteSpace(model.SelectionId)
            ? null
            : await _selectionStore.GetAsync(model.SelectionId, HttpContext.RequestAborted);
        if (user is null || pending is null || pending.ExpiresAt <= DateTimeOffset.UtcNow ||
            !string.Equals(pending.UserId, user.Id, StringComparison.Ordinal))
        {
            _logger.LogWarning("Tenant selection POST expired, replayed, or owned by another user. SelectionId: {SelectionId}. TraceIdentifier: {TraceIdentifier}",
                model.SelectionId, HttpContext.TraceIdentifier);
            return View("TenantSelectionExpired");
        }

        var memberships = await _membershipService.GetActiveMembershipsAsync(user, HttpContext.RequestAborted);
        var options = memberships.Where(m => pending.AllowedTenantUids.Contains(m.TenantUid)).ToArray();
        var selected = model.SelectedTenantUid is Guid tenantUid
            ? options.SingleOrDefault(m => m.TenantUid == tenantUid)
            : null;
        if (selected is null)
        {
            if (model.SelectedTenantUid is Guid requestedTenantUid && requestedTenantUid != Guid.Empty)
            {
                await _securityAudit.TryRecordInvalidMembershipAsync(
                    HttpContext,
                    user.Id,
                    requestedTenantUid);
            }

            _logger.LogWarning("Submitted tenant was unavailable or not allowed for user {UserId}, selection {SelectionId}. TraceIdentifier: {TraceIdentifier}",
                user.Id, pending.SelectionId, HttpContext.TraceIdentifier);
            ModelState.AddModelError(nameof(model.SelectedTenantUid), "The selected clinic is unavailable. Please try again.");
            return View(new TenantSelectionViewModel
            {
                SelectionId = pending.SelectionId,
                SelectedTenantUid = model.SelectedTenantUid,
                Tenants = options.Select(m => new TenantSelectionOptionViewModel(m.TenantUid, m.TenantKey, m.TenantDisplayName)).ToArray()
            });
        }

        var consumed = await _selectionStore.TakeAsync(pending.SelectionId, HttpContext.RequestAborted);
        if (consumed is null)
        {
            _logger.LogWarning("Tenant selection replay rejected for user {UserId}, selection {SelectionId}. TraceIdentifier: {TraceIdentifier}",
                user.Id, pending.SelectionId, HttpContext.TraceIdentifier);
            return View("TenantSelectionExpired");
        }

        var continuationId = WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        await _selectionStore.StoreContinuationAsync(new TenantSelectionContinuation(
            continuationId, user.Id, pending.ReturnUrl, selected.TenantUid, DateTimeOffset.UtcNow.AddMinutes(2)),
            HttpContext.RequestAborted);
        var resumeUrl = QueryHelpers.AddQueryString(pending.ReturnUrl, "tenant_continuation", continuationId);
        if (!Url.IsLocalUrl(resumeUrl))
            return View("TenantSelectionExpired");

        _logger.LogInformation("Tenant selected successfully for user {UserId}, selection {SelectionId}, tenant {TenantUid}. TraceIdentifier: {TraceIdentifier}",
            user.Id, pending.SelectionId, selected.TenantUid, HttpContext.TraceIdentifier);
        return LocalRedirect(resumeUrl);
    }
}
