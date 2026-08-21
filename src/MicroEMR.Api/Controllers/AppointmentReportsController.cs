using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.Reporting;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.SecurityAudit;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission(PermissionKeys.ReportsView)]
[Route("api/reports/appointments/status")]
public sealed class AppointmentReportsController(
    IAppointmentStatusReportService service,
    IStructuredReadAuditService readAudit,
    ILogger<AppointmentReportsController> logger) : ControllerBase
{
    [HttpGet]
    [SensitiveCapability(SecurityAuditCapabilities.AppointmentReportRun)]
    public async Task<ActionResult<AppointmentStatusReport>> Get([FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate, [FromQuery] bool auditExecution = true,
        CancellationToken cancellationToken = default)
    {
        if (startDate == default) ModelState.AddModelError(nameof(startDate), "Start date is required.");
        if (endDate == default) ModelState.AddModelError(nameof(endDate), "End date is required.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var report = await service.GetAsync(startDate, endDate, cancellationToken);
            if (auditExecution)
                await readAudit.RecordAggregateReportAsync(ReadAuditActions.ReportExecuted,
                    ReadAuditReportKeys.AppointmentStatusDateReport, HttpContext.TraceIdentifier, cancellationToken);
            return Ok(report);
        }
        catch (ArgumentException exception) { ModelState.AddModelError("dateRange", exception.Message); return ValidationProblem(ModelState); }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Appointment report audit failed; report disclosure was prevented. TraceIdentifier: {TraceIdentifier}.", HttpContext.TraceIdentifier);
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Report audit unavailable",
                detail: "The report cannot be displayed because access auditing is temporarily unavailable.");
        }
    }

    [HttpGet("csv")]
    [RequirePermission(PermissionKeys.ReportsExport)]
    [SensitiveCapability(SecurityAuditCapabilities.AppointmentReportExport)]
    public async Task<IActionResult> Csv([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        if (startDate == default || endDate == default) return BadRequest(new { message = "Start date and end date are required." });
        try
        {
            var report = await service.GetAsync(startDate, endDate, cancellationToken);
            var csv = service.CreateCsv(report);
            await readAudit.RecordAggregateReportAsync(ReadAuditActions.CsvExported,
                ReadAuditReportKeys.AppointmentStatusDateReport, HttpContext.TraceIdentifier, cancellationToken);
            return File(csv, "text/csv; charset=utf-8",
                $"appointment-status-report-{startDate:yyyy-MM-dd}-to-{endDate:yyyy-MM-dd}.csv");
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Appointment CSV export audit failed; export disclosure was prevented. TraceIdentifier: {TraceIdentifier}.", HttpContext.TraceIdentifier);
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "CSV export audit unavailable",
                detail: "The CSV cannot be exported because access auditing is temporarily unavailable.");
        }
    }
}
