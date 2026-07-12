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
                    Status = appointment.Status
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
