using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.Providers;

namespace MicroEMR.Web.Controllers;

[Authorize, RequireWebPermission(PermissionKeys.PatientsEdit)]
[RequireWebPermission(PermissionKeys.PatientsView)]
[RequireWebPermission(PermissionKeys.ProvidersView)]
public sealed class PatientRegistrationProvidersController(
    IProviderAdministrationApiClient providers,
    ILogger<PatientRegistrationProvidersController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        try
        {
            var active = await providers.List("Active", cancellationToken);
            return Json(active.Select(item => new { item.ProviderUid, item.DisplayName, item.Specialty }));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Active providers could not be loaded for patient registration.");
            return StatusCode(502, new { message = "Providers could not be loaded." });
        }
    }
}
