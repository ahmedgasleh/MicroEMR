using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models;
using MicroEMR.Web.Models.TenantUserAdministration;
using MicroEMR.Web.Services.TenantUserAdministration;

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
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "User administration could not be loaded.");
            return View("Error", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
        }
    }
}
