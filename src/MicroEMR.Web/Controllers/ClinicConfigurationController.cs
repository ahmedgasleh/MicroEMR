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
    ILogger<ClinicConfigurationController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            return View(await client.GetAsync(cancellationToken));
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
        if (!ModelState.IsValid) return View(model);

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
            return View(latest);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.BadRequest)
        {
            logger.LogWarning(exception, "The clinic settings API rejected the submitted values.");
            ModelState.AddModelError(string.Empty, "Please correct the clinic settings and try again.");
            return View(model);
        }
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Clinic settings could not be saved.");
            ModelState.AddModelError(string.Empty, "Clinic settings could not be saved.");
            return View(model);
        }
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
