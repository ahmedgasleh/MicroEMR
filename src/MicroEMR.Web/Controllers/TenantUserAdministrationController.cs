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
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Tenant membership could not be changed.");
            return StatusCode(502, new { success = false, message = "The membership could not be changed." });
        }
    }
}
