using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.SecurityAudit;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, RequirePermission(PermissionKeys.EncountersView)]
[Route("api/patients/{patientUid:guid}/encounters/{encounterUid:guid}/diagnoses")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class EncounterDiagnosesController(IEncounterDiagnosisService service, ILogger<EncounterDiagnosesController> logger) : ControllerBase
{
    [HttpGet, SensitiveCapability(SecurityAuditCapabilities.EncounterView)]
    public Task<IActionResult> Get(Guid patientUid, Guid encounterUid, CancellationToken token)
        => Run(() => service.GetAsync(patientUid,encounterUid,HttpContext.TraceIdentifier,token),token);
    [HttpPost, RequirePermission(PermissionKeys.EncountersEdit)]
    public Task<IActionResult> Save(Guid patientUid, Guid encounterUid, SaveEncounterDiagnosesRequest request, CancellationToken token)
        => Run(() => service.SaveAsync(patientUid,encounterUid,request,token),token);
    private async Task<IActionResult> Run(Func<Task<EncounterDiagnosesResponse?>> action, CancellationToken token)
    {
        try { var result = await action(); return result is null ? NotFound() : Ok(result); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (ArgumentException error) { return Problem(statusCode:400,detail:error.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ClinicalUserResolutionException) { return Forbid(); }
        catch (EncounterDiagnosisConflictException error) { return Problem(statusCode:409,detail:error.Message); }
        catch (Exception error) { logger.LogError(error,"Encounter diagnosis operation failed."); return Problem(statusCode:503,detail:"Diagnoses could not be saved or loaded. Reload the encounter and retry."); }
    }
}
