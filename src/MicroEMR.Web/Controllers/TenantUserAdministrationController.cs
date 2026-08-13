using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models;
using MicroEMR.Web.Models.TenantUserAdministration;
using MicroEMR.Web.Services.TenantUserAdministration;
using MicroEMR.Application.PlatformAdministration;

namespace MicroEMR.Web.Controllers;

[Authorize(Policy = ClinicConfigurationAuthorization.Policy)]
public sealed class TenantUserAdministrationController(
    ITenantUserAdministrationApiClient client,
    ILogger<TenantUserAdministrationController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            return View(new TenantUserAdministrationViewModel
            {
                Users = await client.GetUsersAsync(cancellationToken)
                , CanonicalRoles = TenantRoleCatalog.Allowed.OrderBy(x => x, StringComparer.Ordinal).ToArray()
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "User administration could not be loaded.");
            return View("Error", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(string authUserId, CancellationToken cancellationToken)
    {
        var user = await client.GetUserAsync(authUserId, cancellationToken);
        if (user is null) return NotFound();
        return View(new TenantUserDetailsViewModel { User = user });
    }

    [HttpGet]
    public IActionResult Add() => View(new AddTenantUserViewModel
    {
        InitialRole = "Physician",
        CanonicalRoles = TenantRoleCatalog.Allowed.OrderBy(x => x, StringComparer.Ordinal).ToArray()
    });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddTenantUserViewModel model, CancellationToken cancellationToken)
    {
        if (!TenantRoleCatalog.Allowed.Contains(model.InitialRole))
            ModelState.AddModelError(nameof(model.InitialRole), "Select a valid initial role.");
        if (!ModelState.IsValid)
            return View(WithRoles());
        try
        {
            var result = await client.AddUserAsync(model, cancellationToken);
            TempData[result.ClinicalProvisioningFailed ? "WarningMessage" : "SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { authUserId = result.User.AuthUserId });
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Conflict)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(WithRoles());
        }
        catch (Exception ex) when (ex is HttpRequestException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Tenant user could not be added.");
            ModelState.AddModelError(string.Empty, "The user could not be added. No duplicate submission is needed; check User Administration before retrying.");
            return View(WithRoles());
        }

        AddTenantUserViewModel WithRoles() => new()
        {
            FirstName = model.FirstName, LastName = model.LastName, Email = model.Email,
            InitialRole = model.InitialRole, ProvisionClinicalUser = model.ProvisionClinicalUser,
            CanonicalRoles = TenantRoleCatalog.Allowed.OrderBy(x => x, StringComparer.Ordinal).ToArray()
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFromModal(AddTenantUserViewModel model, CancellationToken cancellationToken)
    {
        if (!TenantRoleCatalog.Allowed.Contains(model.InitialRole))
            ModelState.AddModelError(nameof(model.InitialRole), "Select a valid initial role.");
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            return BadRequest(new { success = false, message = errors.FirstOrDefault() ?? "Check the highlighted fields." });
        }
        try
        {
            var result = await client.AddUserAsync(model, cancellationToken);
            return Json(new { success = true, result.Message, result.ClinicalProvisioningFailed });
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Conflict)
        { return StatusCode((int)ex.StatusCode.Value, new { success = false, message = ex.Message }); }
        catch (Exception ex) when (ex is HttpRequestException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Tenant user could not be added from the modal.");
            return StatusCode(502, new { success = false, message = "The user could not be added. Check the list before retrying." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Deactivate(string authUserId, string rowVersion, CancellationToken cancellationToken) =>
        ChangeAsync(() => client.DeactivateAsync(authUserId, rowVersion, cancellationToken), cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Activate(string authUserId, string rowVersion, CancellationToken cancellationToken) =>
        ChangeAsync(() => client.ActivateAsync(authUserId, rowVersion, cancellationToken), cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateRoles(string authUserId, string rowVersion, string[] selectedRoles,
        CancellationToken cancellationToken) =>
        ChangeAsync(() => client.UpdateRolesAsync(authUserId, selectedRoles, rowVersion, cancellationToken), cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ProvisionClinicalUser(string authUserId, CancellationToken cancellationToken) =>
        ChangeAsync(() => client.ProvisionClinicalUserAsync(authUserId, cancellationToken), cancellationToken);

    private async Task<IActionResult> ChangeAsync(
        Func<Task<TenantUserAdministrationItemViewModel>> action,
        CancellationToken cancellationToken)
    {
        try { return Json(new { success = true, user = await action() }); }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            logger.LogWarning(exception, "Tenant membership change was rejected.");
            return Conflict(new { success = false, message = exception.Message,
                users = await client.GetUsersAsync(cancellationToken) });
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        { return NotFound(new { success = false, message = exception.Message }); }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.BadRequest)
        { return BadRequest(new { success = false, message = exception.Message }); }
        catch (HttpRequestException exception) when (exception.StatusCode is
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        { return StatusCode((int)exception.StatusCode.Value, new { success = false, message = exception.Message }); }
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Tenant membership could not be changed.");
            return StatusCode(502, new { success = false, message = "The membership could not be changed." });
        }
    }
}
