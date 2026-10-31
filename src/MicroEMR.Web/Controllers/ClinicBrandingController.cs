using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicConfiguration;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.ClinicConfiguration;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class ClinicBrandingController(IClinicBrandingApiClient client, ILogger<ClinicBrandingController> logger) : Controller
{
    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicSettingsManage)]
    [RequestSizeLimit(ClinicBrandingService.MaximumLogoBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = ClinicBrandingService.MaximumLogoBytes)]
    public async Task<IActionResult> Save(IFormFile? logo, bool remove, string? rowVersion, CancellationToken token)
    {
        try
        {
            if (logo?.Length > ClinicBrandingService.MaximumLogoBytes || (remove && logo is not null) || (!remove && logo is null))
            { TempData["BrandingError"] = "Choose a PNG or JPEG up to 1 MiB, or remove the current logo."; }
            else
            {
                await client.SaveAsync(logo, remove, rowVersion, token);
                TempData["SuccessMessage"] = remove ? "Clinic logo removed." : "Clinic logo saved.";
            }
        }
        catch (HttpRequestException error)
        {
            logger.LogWarning(error, "Clinic logo save was unsuccessful.");
            if (error.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized) return StatusCode(403);
            TempData["BrandingError"] = error.StatusCode == HttpStatusCode.Conflict
                ? "Clinic branding changed. Review the latest logo before saving again."
                : "Logo could not be saved. Use a valid PNG or JPEG up to 1 MiB and 2048 pixels on each side, then retry.";
        }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
        return RedirectToAction("Index", "ClinicConfiguration");
    }

    [HttpGet]
    public async Task<IActionResult> Logo(CancellationToken token)
    {
        try
        {
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            var bytes = await client.GetLogoAsync(token);
            return bytes is null ? NotFound() : File(bytes, "image/png");
        }
        catch (HttpRequestException error) { return SafeFailure(error); }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
    }

    [HttpGet]
    public async Task<IActionResult> Preview(CancellationToken token)
    {
        try
        {
            Response.Headers.CacheControl = "private, no-store";
            return Content(await client.GetHeaderAsync(token), "text/html; charset=utf-8");
        }
        catch (HttpRequestException error) { return SafeFailure(error); }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
    }

    private IActionResult SafeFailure(HttpRequestException error)
    {
        logger.LogWarning(error, "Clinic branding could not be loaded.");
        return StatusCode(error.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized ? 403 : 502,
            new { message="Clinic branding is unavailable." });
    }
}
