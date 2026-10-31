using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models;
using MicroEMR.Web.Models.ClinicConfiguration;
using MicroEMR.Web.Services.ClinicConfiguration;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Web.Controllers;

[Authorize]
[RequireWebPermission(PermissionKeys.ClinicSettingsManage)]
public sealed class ClinicConfigurationController(
    IClinicConfigurationApiClient client,
    ILogger<ClinicConfigurationController> logger,
    IClinicBrandingApiClient branding) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var model = await client.GetAsync(cancellationToken);
            return await SettingsViewAsync(model, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Clinic settings could not be loaded.");
            return View("Error", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ClinicConfigurationViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return await SettingsViewAsync(model, cancellationToken);

        try
        {
            await client.SaveAsync(ToRequest(model), cancellationToken);
            TempData["SuccessMessage"] = "Clinic settings saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            logger.LogWarning(exception, "A stale clinic settings update was rejected.");
            var latest = await client.GetAsync(cancellationToken);
            ModelState.Clear();
            ModelState.AddModelError(string.Empty,
                "Clinic settings were changed by another user. The latest values have been reloaded.");
            return await SettingsViewAsync(latest, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.BadRequest)
        {
            logger.LogWarning(exception, "The clinic settings API rejected the submitted values.");
            ModelState.AddModelError(string.Empty, "Please correct the clinic settings and try again.");
            return await SettingsViewAsync(model, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Clinic settings could not be saved.");
            ModelState.AddModelError(string.Empty, "Clinic settings could not be saved.");
            return await SettingsViewAsync(model, cancellationToken);
        }
    }

    private async Task<ViewResult> SettingsViewAsync(ClinicConfigurationViewModel model, CancellationToken token)
    {
        model.Branding = await branding.GetStateAsync(token);
        return View("Index", model);
    }

    private static SaveClinicConfigurationRequest ToRequest(ClinicConfigurationViewModel model) => new()
    {
        LegalName = model.LegalName,
        Phone = model.Phone,
        Fax = model.Fax,
        Email = model.Email,
        AddressLine1 = model.AddressLine1,
        AddressLine2 = model.AddressLine2,
        City = model.City,
        ProvinceState = model.ProvinceState,
        PostalCode = model.PostalCode,
        Country = model.Country,
        DefaultAppointmentDurationMinutes = model.DefaultAppointmentDurationMinutes,
        RowVersion = model.RowVersion
    };
}
