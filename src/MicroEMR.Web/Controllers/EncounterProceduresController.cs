using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.PatientEncounters;

namespace MicroEMR.Web.Controllers;

[Authorize, RequireWebPermission(PermissionKeys.EncountersView), Route("encounter-procedures")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EncounterProceduresController(IEncounterProcedureApiClient client, ILogger<EncounterProceduresController> logger) : Controller
{
    [HttpGet]
    public Task<IActionResult> Get(Guid patientUid, Guid encounterUid, CancellationToken token) =>
        Run(async () => await client.GetAsync(patientUid, encounterUid, token));
    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Save(Guid patientUid, Guid encounterUid, [FromBody] SaveEncounterProcedureRequest request, CancellationToken token) =>
        ModelState.IsValid ? Run(async () => await client.SaveAsync(patientUid, encounterUid, request, token)) :
            Task.FromResult<IActionResult>(BadRequest(new { message = "Review the procedure fields." }));

    private async Task<IActionResult> Run(Func<Task<object?>> action)
    {
        try { var result = await action(); return result is null ? NotFound() : Json(result); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (HttpRequestException error)
        {
            logger.LogWarning(error, "Encounter procedure API request failed.");
            return StatusCode((int?)error.StatusCode ?? 502, new { message = error.Message });
        }
    }
}
