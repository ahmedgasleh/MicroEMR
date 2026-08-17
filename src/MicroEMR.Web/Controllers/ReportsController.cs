using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models.Reporting;
using MicroEMR.Web.Services.Reporting;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Web.Controllers;

[Authorize]
[RequireWebPermission(PermissionKeys.ReportsView)]
public sealed class ReportsController(IAppointmentStatusReportApiClient client, ILogger<ReportsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> AppointmentStatus(DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = startDate ?? new DateOnly(today.Year, today.Month, 1);
        var end = endDate ?? today;
        if (end < start || end.DayNumber - start.DayNumber > 365)
            return View(new AppointmentStatusReportViewModel { StartDate = start, EndDate = end,
                ErrorMessage = end < start ? "End date must be on or after start date." : "The report range cannot exceed 366 days." });
        try { return View(new AppointmentStatusReportViewModel { StartDate = start, EndDate = end,
            Report = await client.GetAsync(start, end, startDate.HasValue || endDate.HasValue, cancellationToken) }); }
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        { logger.LogError(exception, "Appointment status report could not be loaded."); return View(new AppointmentStatusReportViewModel
            { StartDate = start, EndDate = end, ErrorMessage = "The appointment status report could not be loaded." }); }
    }

    [HttpGet]
    [RequireWebPermission(PermissionKeys.ReportsExport)]
    public async Task<IActionResult> AppointmentStatusCsv(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        if (endDate < startDate || endDate.DayNumber - startDate.DayNumber > 365) return BadRequest();
        try { return File(await client.GetCsvAsync(startDate, endDate, cancellationToken), "text/csv; charset=utf-8",
            $"appointment-status-report-{startDate:yyyy-MM-dd}-to-{endDate:yyyy-MM-dd}.csv"); }
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        { logger.LogError(exception, "Appointment status CSV could not be exported."); return StatusCode(502); }
    }
}
