using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.PatientEncounters;

namespace MicroEMR.Web.Controllers;

[Authorize, RequireWebPermission(PermissionKeys.EncountersView), Route("encounter-diagnoses")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class EncounterDiagnosesController(IEncounterDiagnosisApiClient client, ILogger<EncounterDiagnosesController> logger) : Controller
{
    [HttpGet]
    public Task<IActionResult> Get(Guid patientUid, Guid encounterUid, CancellationToken token)
        => Run(patientUid,encounterUid,null,token);
    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.EncountersEdit)]
    public Task<IActionResult> Save(Guid patientUid, Guid encounterUid, [FromBody] SaveEncounterDiagnosesRequest request, CancellationToken token)
        => ModelState.IsValid ? Run(patientUid,encounterUid,request,token) : Task.FromResult<IActionResult>(BadRequest(new {message="Invalid diagnosis choices."}));
    private async Task<IActionResult> Run(Guid patient, Guid encounter, SaveEncounterDiagnosesRequest? input, CancellationToken token)
    {
        if (patient == Guid.Empty || encounter == Guid.Empty) return BadRequest();
        try { var result = await client.SendAsync(patient,encounter,input,token); return result is null ? NotFound() : Json(result); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (HttpRequestException error) { logger.LogWarning(error,"Encounter diagnosis API request failed."); return StatusCode((int?)error.StatusCode ?? 502,new {message=error.Message}); }
    }
}
