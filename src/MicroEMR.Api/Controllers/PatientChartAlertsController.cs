using MicroEMR.Api.ClinicalUsers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientChartAlerts;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Api.Authorization;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, RequirePermission(PermissionKeys.PatientsView)]
[Route("api/patients/{patientUid:guid}/chart-alerts")]
public sealed class PatientChartAlertsController(IPatientChartAlertService service, IPatientChartReadAuditService readAudit, ILogger<PatientChartAlertsController> logger) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(Guid patientUid, string status = "Active", CancellationToken t = default) => Run(async () =>
    {
        var rows = await service.ListAsync(patientUid, status, t);
        if (rows.Any(x => x.PatientUid != patientUid)) return Forbid();
        await readAudit.RecordOpenedAsync(patientUid, HttpContext.TraceIdentifier, t);
        return Ok(rows);
    });
    [HttpGet("{uid:guid}")]
    public Task<IActionResult> Get(Guid patientUid, Guid uid, CancellationToken t) => Run(async () =>
    {
        var row = await service.GetAsync(patientUid, uid, t);
        if (row is null) return NotFound();
        if (row.PatientUid != patientUid) return Forbid();
        await readAudit.RecordOpenedAsync(patientUid, HttpContext.TraceIdentifier, t);
        return Ok(row);
    });
    [HttpPost, RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Create(Guid patientUid, CreatePatientChartAlertRequest x, CancellationToken t)
    {
        x.RequestCorrelationId = HttpContext.TraceIdentifier;
        return Run(async () => await service.CreateAsync(patientUid, x, ClinicalUserActorContext.GetRequired(HttpContext), t) is {} row
            ? CreatedAtAction(nameof(Get), new { patientUid, uid = row.PatientChartAlertUid }, row) : NotFound());
    }
    [HttpPut("{uid:guid}"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Update(Guid patientUid, Guid uid, UpdatePatientChartAlertRequest x, CancellationToken t)
    {
        x.RequestCorrelationId = HttpContext.TraceIdentifier;
        return Run(async () => await service.UpdateAsync(patientUid, uid, x, ClinicalUserActorContext.GetRequired(HttpContext), t) is {} row ? Ok(row) : NotFound());
    }
    [HttpPost("{uid:guid}/resolve"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Resolve(Guid patientUid, Guid uid, ResolvePatientChartAlertRequest x, CancellationToken t)
    {
        x.RequestCorrelationId = HttpContext.TraceIdentifier;
        return Run(async () => await service.ResolveAsync(patientUid, uid, x, ClinicalUserActorContext.GetRequired(HttpContext), t) is {} row ? Ok(row) : NotFound());
    }
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ChartAlertValidationException e) { return BadRequest(new { message = e.Message }); }
        catch (ChartAlertNotFoundException) { return NotFound(); }
        catch (ChartAlertConcurrencyException e) { return Conflict(new { message = e.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception e) { logger.LogError(e, "Chart Alert operation failed."); return StatusCode(503, new { message = "Alerts could not be processed. Reload and review before retrying." }); }
    }
}
