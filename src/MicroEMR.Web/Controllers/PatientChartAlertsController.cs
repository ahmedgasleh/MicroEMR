using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models.PatientChartAlerts;
using MicroEMR.Web.Services.PatientChartAlerts;

namespace MicroEMR.Web.Controllers;

[Authorize, RequireWebPermission(PermissionKeys.PatientsView)]
public sealed class PatientChartAlertsController(IPatientChartAlertApiClient client, ILogger<PatientChartAlertsController> logger) : Controller
{
    [HttpGet]
    public Task<IActionResult> List(Guid patientUid, string status = "Active", CancellationToken t = default) => Run(async () =>
    {
        if (patientUid == Guid.Empty) return BadRequest();
        var rows = await client.List(patientUid, status, t);
        if (rows.Any(x => x.PatientUid != patientUid)) return StatusCode(403, new { success = false, message = "Alert patient context is unavailable." });
        return Json(new { success = true, alerts = rows });
    });
    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Create(SavePatientChartAlertViewModel x, CancellationToken t) => Run(async () =>
    {
        if (x.PatientUid == Guid.Empty || !ModelState.IsValid) return BadRequest(new { success = false, message = "Review the alert fields." });
        return Result(await client.Create(x.PatientUid, x, t));
    });
    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Update(UpdatePatientChartAlertViewModel x, CancellationToken t) => Run(async () =>
    {
        if (x.PatientUid == Guid.Empty || x.PatientChartAlertUid == Guid.Empty || !ModelState.IsValid) return BadRequest(new { success = false, message = "Review the alert and reload before saving." });
        return Result(await client.Update(x.PatientUid, x.PatientChartAlertUid, x, t));
    });
    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Resolve(ResolvePatientChartAlertViewModel x, CancellationToken t) => Run(async () =>
    {
        if (x.PatientUid == Guid.Empty || x.PatientChartAlertUid == Guid.Empty || !ModelState.IsValid) return BadRequest(new { success = false, message = "Reload and review the alert before resolving." });
        return Result(await client.Resolve(x.PatientUid, x.PatientChartAlertUid, x, t));
    });
    private IActionResult Result(PatientChartAlertResponse? row) => row is null ? NotFound(new { success = false, message = "Alert not found." }) : Json(new { success = true, alert = row });
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception e) when (e is HttpRequestException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Chart Alert request was rejected or unavailable.");
            var status = e is HttpRequestException h && h.StatusCode is {} code ? (int)code : e is UnauthorizedAccessException ? 401 : 503;
            return StatusCode(status, new { success = false, message = status == 409 ? "The alert changed or is resolved. Reload and review before saving again." : status is 401 or 403 ? "You do not have access to this Alert operation." : "Alerts could not be processed. Reload and review." });
        }
    }
}
