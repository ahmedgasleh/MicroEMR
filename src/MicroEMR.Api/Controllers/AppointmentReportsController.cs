using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.Reporting;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission(PermissionKeys.ReportsView)]
[Route("api/reports/appointments/status")]
public sealed class AppointmentReportsController(IAppointmentStatusReportService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AppointmentStatusReport>> Get([FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate, CancellationToken cancellationToken)
    {
        if (startDate == default) ModelState.AddModelError(nameof(startDate), "Start date is required.");
        if (endDate == default) ModelState.AddModelError(nameof(endDate), "End date is required.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try { return Ok(await service.GetAsync(startDate, endDate, cancellationToken)); }
        catch (ArgumentException exception) { ModelState.AddModelError("dateRange", exception.Message); return ValidationProblem(ModelState); }
    }

    [HttpGet("csv")]
    [RequirePermission(PermissionKeys.ReportsExport)]
    public async Task<IActionResult> Csv([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        if (startDate == default || endDate == default) return BadRequest(new { message = "Start date and end date are required." });
        try
        {
            var report = await service.GetAsync(startDate, endDate, cancellationToken);
            return File(service.CreateCsv(report), "text/csv; charset=utf-8",
                $"appointment-status-report-{startDate:yyyy-MM-dd}-to-{endDate:yyyy-MM-dd}.csv");
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
