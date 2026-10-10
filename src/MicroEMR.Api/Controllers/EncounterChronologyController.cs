using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientEncounters.Chronology;
using MicroEMR.Application.SecurityAudit;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, RequirePermission(PermissionKeys.PatientsView), RequirePermission(PermissionKeys.EncountersView)]
[Route("api/patients/{patientUid:guid}/encounter-chronology")]
public sealed class EncounterChronologyController(IEncounterChronologyService service, ILogger<EncounterChronologyController> logger) : ControllerBase
{
    [HttpGet, SensitiveCapability(SecurityAuditCapabilities.EncounterView)]
    public async Task<IActionResult> Get(Guid patientUid, [FromQuery] EncounterChronologyRequest request, CancellationToken token)
    {
        try
        {
            Response.Headers.CacheControl = "no-store";
            var result = await service.GetAsync(patientUid, request, HttpContext.TraceIdentifier, token);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException) { return BadRequest(new { message = "Select a valid patient, date range, direction and time zone." }); }
        catch (UnauthorizedAccessException error)
        {
            logger.LogWarning(error, "Chronological content access denied. TraceIdentifier: {TraceIdentifier}.", HttpContext.TraceIdentifier);
            return Forbid();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            logger.LogError(error, "Chronological content could not be disclosed for patient {PatientUid}.", patientUid);
            return Problem(statusCode: 503, title: "Chronological content unavailable", detail: "Required source content or access auditing is unavailable. Retry before printing.");
        }
    }

    [HttpGet("encounters/{encounterUid:guid}/final-pdf"), SensitiveCapability(SecurityAuditCapabilities.EncounterView)]
    public async Task<IActionResult> EncounterPdf(Guid patientUid, Guid encounterUid, CancellationToken token)
    {
        try
        {
            Response.Headers.CacheControl = "no-store";
            var result = await service.OpenEncounterPdfAsync(patientUid, encounterUid, HttpContext.TraceIdentifier, token);
            return result is null ? NotFound() : File(result.Content, result.MimeType, result.FileName);
        }
        catch (UnauthorizedAccessException error)
        {
            logger.LogWarning(error, "Chronology encounter attachment access denied. TraceIdentifier: {TraceIdentifier}.", HttpContext.TraceIdentifier);
            return Forbid();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            logger.LogError(error, "Chronology encounter attachment could not be disclosed for patient {PatientUid}.", patientUid);
            return Problem(statusCode: 503, title: "Encounter attachment unavailable");
        }
    }
}
