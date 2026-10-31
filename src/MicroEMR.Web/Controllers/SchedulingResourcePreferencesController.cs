using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Scheduling.Preferences;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.Scheduling;

namespace MicroEMR.Web.Controllers;

[Authorize, RequireWebPermission(PermissionKeys.SchedulingView), Route("Scheduling/ResourcePreferences")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SchedulingResourcePreferencesController(
    ISchedulingResourcePreferencesApiClient client, ILogger<SchedulingResourcePreferencesController> logger) : Controller
{
    [HttpGet]
    public Task<IActionResult> Get(CancellationToken token) => Execute(() => client.GetAsync(token));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Save([FromBody] SaveSchedulingResourcePreferencesRequest request, CancellationToken token) =>
        ModelState.IsValid ? Execute(() => client.SaveAsync(request, token))
            : Task.FromResult<IActionResult>(BadRequest(new { message = "Select at least one valid scheduling resource." }));

    private async Task<IActionResult> Execute(Func<Task<SchedulingResourcePreferencesResponse>> action)
    {
        try { return Json(await action()); }
        catch (SchedulingResourcePreferencesConcurrencyException error) { return Conflict(new { message = error.Message }); }
        catch (UnauthorizedAccessException) { return StatusCode(401); }
        catch (HttpRequestException error) when (error.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        { return StatusCode((int)error.StatusCode.Value); }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.BadRequest)
        { return BadRequest(new { message = "Select at least one available scheduling resource." }); }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            logger.LogWarning(error, "Scheduling resource defaults request failed.");
            return StatusCode(503, new { message = "Resource defaults are temporarily unavailable." });
        }
    }
}
