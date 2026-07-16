using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models;
using Microsoft.AspNetCore.Authorization;
using MicroEMR.Web.Models.Dashboard;
using MicroEMR.Web.Services.Scheduling;

namespace MicroEMR.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private static readonly HashSet<string> AllowedAppointmentStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Scheduled", "Arrived", "Roomed", "Seen", "Completed"
        };
    private readonly ISchedulingApiClient _schedulingApiClient;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        ISchedulingApiClient schedulingApiClient,
        ILogger<HomeController> logger)
    {
        _schedulingApiClient = schedulingApiClient;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new DashboardViewModel();
        var todayLocal = DateTime.Today;
        var tomorrowLocal = todayLocal.AddDays(1);

        try
        {
            var appointments = await _schedulingApiClient.GetAppointmentsAsync(
                todayLocal.ToUniversalTime(),
                tomorrowLocal.ToUniversalTime(),
                resourceUid: null,
                cancellationToken);

            var activeAppointments = appointments
                .Where(appointment => !string.Equals(
                    appointment.Status,
                    "Cancelled",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(appointment => appointment.StartDateTimeUtc)
                .ToArray();

            model.TodaysAppointmentCount = activeAppointments.Length;
            model.TodaysAppointments = activeAppointments
                .Take(10)
                .Select(appointment => new DashboardAppointmentViewModel
                {
                    AppointmentUid = appointment.AppointmentUid,
                    PatientUid = appointment.PatientUid,
                    PatientDisplayName = appointment.PatientDisplayName,
                    ChartNumber = appointment.ChartNumber,
                    StartDateTimeLocal = NormalizeUtc(appointment.StartDateTimeUtc).ToLocalTime(),
                    EndDateTimeLocal = NormalizeUtc(appointment.EndDateTimeUtc).ToLocalTime(),
                    PrimaryResourceName = appointment.PrimaryResourceName,
                    AppointmentType = appointment.AppointmentType,
                    Reason = appointment.Reason,
                    Status = string.Equals(appointment.Status, "Booked", StringComparison.OrdinalIgnoreCase)
                        ? "Scheduled"
                        : appointment.Status
                })
                .ToArray();
        }
        catch (Exception exception)
        {
            model.ScheduleLoadFailed = true;
            _logger.LogError(exception, "Unable to load today's dashboard schedule.");
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAppointmentStatus(
        UpdateDashboardAppointmentStatusViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.AppointmentUid == Guid.Empty)
            ModelState.AddModelError(nameof(model.AppointmentUid), "Appointment identifier is required.");
        if (!AllowedAppointmentStatuses.Contains(model.Status))
            ModelState.AddModelError(nameof(model.Status), "Invalid appointment status.");
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Invalid appointment status." });

        try
        {
            var result = await _schedulingApiClient.UpdateAppointmentStatusAsync(
                model.AppointmentUid,
                new Models.Scheduling.UpdateAppointmentStatusRequest { Status = model.Status },
                cancellationToken);
            if (result is null)
                return NotFound(new { success = false, message = "Appointment was not found." });

            return Json(new
            {
                success = true,
                message = "Appointment status updated.",
                status = result.Status
            });
        }
        catch (AppointmentStatusConflictException)
        {
            return Conflict(new
            {
                success = false,
                message = "Cancelled appointments cannot be updated."
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to update a dashboard appointment status.");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                message = "Appointment status could not be updated."
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartEncounter(
        Guid appointmentUid,
        CancellationToken cancellationToken)
    {
        if (appointmentUid == Guid.Empty)
        {
            TempData["ErrorMessage"] = "Encounter could not be started.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var result = await _schedulingApiClient.StartEncounterFromAppointmentAsync(
                appointmentUid, cancellationToken);
            if (result is null)
            {
                TempData["ErrorMessage"] = "Encounter could not be started.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = result.WasCreated
                ? "Encounter started."
                : "Existing encounter opened.";
            return RedirectToAction(
                "Details",
                "Patients",
                new
                {
                    patientUid = result.PatientUid,
                    tab = "encounters",
                    encounterUid = result.EncounterUid
                });
        }
        catch (StartEncounterConflictException exception)
        {
            TempData["ErrorMessage"] = exception.IsCompleted
                ? "Completed appointments cannot start a new encounter."
                : "Cancelled appointments cannot start encounters.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to start an encounter from a dashboard appointment.");
            TempData["ErrorMessage"] = "Encounter could not be started.";
            return RedirectToAction(nameof(Index));
        }
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
