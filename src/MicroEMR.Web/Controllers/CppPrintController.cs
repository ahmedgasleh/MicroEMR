using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.Patients;

namespace MicroEMR.Web.Controllers;

[Authorize, RequireWebPermission(PermissionKeys.PatientsView), Route("cpp/print")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CppPrintController(ICppPrintApiClient client, ILogger<CppPrintController> logger) : Controller
{
    [HttpGet("options")]
    public async Task<IActionResult> Options(Guid patientUid,CancellationToken token)
    {
        try { return Json(await client.GetOptionsAsync(patientUid,token)); }
        catch (Exception error) when (error is HttpRequestException or UnauthorizedAccessException) { return Failure(error); }
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Print([FromForm] Guid patientUid,[FromForm] CppPrintRequest request,CancellationToken token)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = "Invalid CPP print choices." });
        try
        {
            var bytes = await client.PrintAsync(patientUid,request,token);
            return bytes is null ? NotFound() : File(bytes,"application/pdf");
        }
        catch (Exception error) when (error is HttpRequestException or UnauthorizedAccessException) { return Failure(error); }
    }
    private IActionResult Failure(Exception error)
    {
        logger.LogWarning(error,"CPP print API request rejected.");
        var status = error is HttpRequestException http ? (int?)http.StatusCode ?? 502 : 403;
        return StatusCode(status,new { message = error.Message });
    }
}
