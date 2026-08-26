using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Cds;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.Cds;

namespace MicroEMR.Web.Controllers;

[Authorize, RequireWebPermission(PermissionKeys.PatientsView)]
[Route("patients/{patientUid:guid}/cds")]
public sealed class PatientCdsController(ICdsApiClient client, ILogger<PatientCdsController> logger) : Controller
{
    [HttpPost("evaluate"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Evaluate(Guid patientUid, CancellationToken cancellationToken)
    {
        try { return Json(new { success = true, result = await client.EvaluateAsync(patientUid, cancellationToken) }); }
        catch
        {
            logger.LogWarning("CDS panel request failed safely.");
            return StatusCode(503, new { success = false, message = "Clinical decision support is temporarily unavailable. The Patient Chart remains available." });
        }
    }

    [HttpGet("{alertUid:guid}/history")]
    public async Task<IActionResult> History(Guid patientUid, Guid alertUid, CancellationToken cancellationToken)
    {
        try { return Json(new { success = true, items = await client.HistoryAsync(patientUid, alertUid, cancellationToken) }); }
        catch { return StatusCode(503, new { success = false, message = "CDS history is temporarily unavailable." }); }
    }

    [HttpPost("{alertUid:guid}/acknowledge"), ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public async Task<IActionResult> Acknowledge(Guid patientUid, Guid alertUid,
        [FromBody] AcknowledgeCdsAlertRequest request, CancellationToken cancellationToken) =>
        await Respond(() => client.AcknowledgeAsync(patientUid, alertUid, request, cancellationToken));

    [HttpPost("{alertUid:guid}/dismiss"), ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public async Task<IActionResult> Dismiss(Guid patientUid, Guid alertUid,
        [FromBody] DismissCdsAlertRequest request, CancellationToken cancellationToken) =>
        await Respond(() => client.DismissAsync(patientUid, alertUid, request, cancellationToken));

    private async Task<IActionResult> Respond(Func<Task<CdsAlertResponse?>> action)
    {
        try { return await action() is { } item ? Json(new { success = true, item }) : NotFound(new { success = false }); }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.Conflict)
        { return Conflict(new { success = false, message = exception.Message }); }
        catch { return StatusCode(503, new { success = false, message = "The CDS response could not be completed." }); }
    }
}
