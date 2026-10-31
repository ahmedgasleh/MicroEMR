using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicConfiguration;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/clinic-branding")]
public sealed class ClinicBrandingController(ClinicBrandingService service, ILogger<ClinicBrandingController> logger) : ControllerBase
{
    [HttpGet, RequirePermission(PermissionKeys.ClinicSettingsManage)]
    public Task<IActionResult> State(CancellationToken token) => Execute(async()=>Ok(await service.GetStateAsync(token)));

    [HttpGet("logo")]
    public Task<IActionResult> Logo(CancellationToken token) => Execute(async()=>
    {
        var logo = await service.GetLogoAsync(token);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return logo is null ? NotFound() : File(logo.PngBytes, "image/png");
    });

    [HttpGet("header")]
    public Task<IActionResult> Header(CancellationToken token) => Execute(async()=>
    {
        Response.Headers.CacheControl = "private, no-store";
        return Content(ClinicPrintHeaderRenderer.RenderDocument(await service.GetPrintHeaderAsync("Clinic print header preview", token)), "text/html; charset=utf-8");
    });

    [HttpPost("logo"), RequirePermission(PermissionKeys.ClinicSettingsManage)]
    [RequestSizeLimit(ClinicBrandingService.MaximumLogoBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = ClinicBrandingService.MaximumLogoBytes)]
    public Task<IActionResult> Save([FromForm] IFormFile? logo, [FromForm] bool remove, [FromForm] string? rowVersion, CancellationToken token) => Execute(async()=>
    {
        byte[]? bytes = null;
        if (logo is not null)
        {
            if (logo.Length > ClinicBrandingService.MaximumLogoBytes) throw new ArgumentException("Choose a logo up to 1 MiB.");
            await using var content = new MemoryStream();
            await logo.CopyToAsync(content, token);
            bytes = content.ToArray();
        }
        return Ok(await service.SaveLogoAsync(bytes, remove, rowVersion, token));
    });

    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (UnauthorizedAccessException) { return StatusCode(403, new { message="Clinic branding access is denied." }); }
        catch (ClinicConfigurationConcurrencyException) { return Conflict(new { message="Clinic branding changed. Reload Clinic Settings before saving." }); }
        catch (FormatException) { return BadRequest(new { message="The branding version is invalid. Reload Clinic Settings." }); }
        catch (ArgumentException) { return BadRequest(new { message="Use a valid PNG or JPEG up to 1 MiB and 2048 pixels on each side. Choose upload or removal, then retry." }); }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            logger.LogError(error, "Clinic branding operation failed.");
            return Problem("Clinic branding is temporarily unavailable.");
        }
    }
}
