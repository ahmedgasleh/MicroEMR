using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.SecurityAudit;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission(PermissionKeys.EncountersView)]
public sealed class EncounterContributionsController(IEncounterContributionService contributions,
    ILogger<EncounterContributionsController> logger) : ControllerBase
{
    [HttpPost("api/patients/{patientUid:guid}/encounters/{encounterUid:guid}/contributions")]
    [RequirePermission(PermissionKeys.EncountersEdit)]
    [SensitiveCapability(SecurityAuditCapabilities.EncounterEdit)]
    public async Task<IActionResult> Save(Guid patientUid, Guid encounterUid,
        SaveEncounterContributionRequest request, CancellationToken token)
    {
        try
        {
            return await contributions.SaveAsync(patientUid, encounterUid, request,
                ClinicalUserActorContext.GetRequired(HttpContext), token) ? Ok() : NotFound();
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (EncounterNoteNotEditableException exception) { return Conflict(new { message = exception.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not save contribution for encounter {EncounterUid}.", encounterUid);
            return Problem("Contribution could not be saved.", statusCode: 503);
        }
    }
}
