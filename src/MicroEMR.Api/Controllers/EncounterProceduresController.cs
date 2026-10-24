using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.SecurityAudit;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, RequirePermission(PermissionKeys.EncountersView)]
[Route("api/patients/{patientUid:guid}/encounters/{encounterUid:guid}/procedures")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EncounterProceduresController(IEncounterProcedureService service, ILogger<EncounterProceduresController> logger) : ControllerBase
{
    [HttpGet, SensitiveCapability(SecurityAuditCapabilities.EncounterView)]
    public Task<IActionResult> Get(Guid patientUid, Guid encounterUid, CancellationToken token) =>
        Run(async () => await service.GetAsync(patientUid, encounterUid, HttpContext.TraceIdentifier, token), token);

    [HttpPost, RequirePermission(PermissionKeys.EncountersEdit), SensitiveCapability(SecurityAuditCapabilities.EncounterEdit)]
    public Task<IActionResult> Save(Guid patientUid, Guid encounterUid, SaveEncounterProcedureRequest request, CancellationToken token) =>
        Run(async () => await service.SaveAsync(patientUid, encounterUid, request, token), token);

    private async Task<IActionResult> Run(Func<Task<object?>> action, CancellationToken token)
    {
        try { var result = await action(); return result is null ? NotFound() : Ok(result); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (ArgumentException error) { return Problem(statusCode: 400, detail: error.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ClinicalUserResolutionException) { return Forbid(); }
        catch (EncounterProcedureConflictException error) { return Problem(statusCode: 409, detail: error.Message); }
        catch (Exception error)
        {
            logger.LogError(error, "Encounter procedure operation failed.");
            return Problem(statusCode: 503, detail: "Procedure could not be saved or loaded. Reload before retrying.");
        }
    }
}
