using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.Patients;

namespace MicroEMR.Web.Controllers;

[Authorize, RequireWebPermission(PermissionKeys.PatientsView), Route("cpp/display-preferences")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CppDisplayPreferencesController(
    ICppDisplayPreferencesApiClient client,
    ILogger<CppDisplayPreferencesController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try { return Json(await client.GetAsync(cancellationToken)); }
        catch (UnauthorizedAccessException) { return StatusCode(StatusCodes.Status403Forbidden); }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "CPP display preferences could not be loaded.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] SaveCppDisplayPreferencesRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = "Invalid CPP display preferences." });
        try { return Json(await client.SaveAsync(request, cancellationToken)); }
        catch (UnauthorizedAccessException) { return StatusCode(StatusCodes.Status403Forbidden); }
        catch (CppDisplayPreferencesConcurrencyException exception) { return Conflict(new { message = exception.Message }); }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "CPP display preferences could not be saved.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "CPP display preferences could not be saved." });
        }
    }
}
