using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Scheduling.Preferences;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, RequirePermission(PermissionKeys.SchedulingView)]
[Route("api/scheduling/resource-preferences")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SchedulingResourcePreferencesController(
    ISchedulingResourcePreferencesService service, ILogger<SchedulingResourcePreferencesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SchedulingResourcePreferencesResponse>> Get(CancellationToken token)
    {
        try { return Ok(await service.GetAsync(token)); }
        catch (ClinicalUserResolutionException) { return Forbid(); }
    }

    [HttpPut]
    public async Task<ActionResult<SchedulingResourcePreferencesResponse>> Save(
        SaveSchedulingResourcePreferencesRequest request, CancellationToken token)
    {
        try { return Ok(await service.SaveAsync(request, token)); }
        catch (ClinicalUserResolutionException) { return Forbid(); }
        catch (SchedulingResourcePreferencesConcurrencyException error) { return Conflict(new { message = error.Message }); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            logger.LogWarning(error, "Scheduling resource defaults could not be saved.");
            return StatusCode(503, new { message = "Resource defaults could not be saved." });
        }
    }
}
