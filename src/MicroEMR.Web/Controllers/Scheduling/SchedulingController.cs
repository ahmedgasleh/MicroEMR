using MicroEMR.Web.Models.Scheduling;
using MicroEMR.Web.Services.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Services.Patients;
using System.Net;

namespace MicroEMR.Web.Controllers.Scheduling;

[Authorize]
[Route("Scheduling")]
public sealed class SchedulingController : Controller
{
    private readonly ISchedulingApiClient _schedulingApiClient;
    private readonly ILogger<SchedulingController> _logger;
    private readonly IPatientApiClient _patientApiClient;

    public SchedulingController(
        ISchedulingApiClient schedulingApiClient,
        IPatientApiClient patientApiClient,
        ILogger<SchedulingController> logger)
    {
        _schedulingApiClient = schedulingApiClient;
        _patientApiClient = patientApiClient;
        _logger = logger;
    }

    [HttpGet("SearchPatients")]
    public async Task<IActionResult> SearchPatients(string? term, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
            return Json(Array.Empty<object>());

        try
        {
            var result = await _patientApiClient.SearchAsync(term.Trim(), null, 1, 10, cancellationToken);
            return Json(result.Items.Select(patient => new
            {
                patientUid = patient.PatientUid,
                displayName = patient.FullName,
                chartNumber = patient.ChartNumber,
                dateOfBirth = patient.DateOfBirth
            }));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to search patients for appointment scheduling.");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Patient search is unavailable." });
        }
    }

    [HttpPost("CreateAppointment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAppointment(
        CreateScheduleAppointmentViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationJson();

        try
        {
            var appointment = await _schedulingApiClient.CreateAppointmentAsync(
                new CreateScheduleAppointmentRequest
                {
                    PatientUid = model.PatientUid,
                    PrimaryResourceUid = model.PrimaryResourceUid,
                    RoomResourceUid = model.RoomResourceUid,
                    StartDateTimeUtc = ToUtc(model.StartDateTimeLocal),
                    EndDateTimeUtc = ToUtc(model.EndDateTimeLocal),
                    AppointmentType = model.AppointmentType,
                    Reason = model.Reason,
                    Notes = model.Notes
                }, cancellationToken);

            return Json(new { success = true, appointmentUid = appointment.AppointmentUid });
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            return Conflict(new { success = false, message = "The selected time conflicts with another appointment for this resource." });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to create a scheduling appointment.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { success = false, message = "The appointment could not be saved. Please try again." });
        }
    }

    [HttpPost("CancelAppointment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelAppointment(
        CancelAppointmentViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.AppointmentUid == Guid.Empty)
            ModelState.AddModelError(nameof(model.AppointmentUid), "Appointment identifier is required.");
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Appointment could not be cancelled." });

        try
        {
            var result = await _schedulingApiClient.CancelAppointmentAsync(
                model.AppointmentUid,
                new CancelScheduleAppointmentRequest { CancelReason = model.CancelReason },
                cancellationToken);
            if (result is null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Appointment was not found or may have already been removed from the schedule."
                });
            }

            return Json(new { success = true, message = "Appointment cancelled." });
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            return Conflict(new { success = false, message = "The appointment is already cancelled." });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to cancel a scheduling appointment.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { success = false, message = "Appointment could not be cancelled." });
        }
    }

    [HttpPost("UpdateAppointment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAppointment(
        UpdateAppointmentViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.AppointmentUid == Guid.Empty)
            ModelState.AddModelError(nameof(model.AppointmentUid), "Appointment identifier is required.");
        if (model.PrimaryResourceUid == Guid.Empty)
            ModelState.AddModelError(nameof(model.PrimaryResourceUid), "Primary resource is required.");
        if (model.StartDateTimeLocal == default)
            ModelState.AddModelError(nameof(model.StartDateTimeLocal), "Start time is required.");
        if (model.EndDateTimeLocal == default)
            ModelState.AddModelError(nameof(model.EndDateTimeLocal), "End time is required.");
        else if (model.EndDateTimeLocal <= model.StartDateTimeLocal)
            ModelState.AddModelError(nameof(model.EndDateTimeLocal), "End time must be after start time.");
        if (!ModelState.IsValid)
            return ValidationJson();

        try
        {
            var result = await _schedulingApiClient.UpdateAppointmentAsync(
                model.AppointmentUid,
                new UpdateScheduleAppointmentRequest
                {
                    PrimaryResourceUid = model.PrimaryResourceUid,
                    RoomResourceUid = model.RoomResourceUid,
                    StartDateTimeUtc = ToUtc(model.StartDateTimeLocal),
                    EndDateTimeUtc = ToUtc(model.EndDateTimeLocal),
                    AppointmentType = model.AppointmentType,
                    Reason = model.Reason,
                    Notes = model.Notes
                },
                cancellationToken);
            if (result is null)
                return NotFound(new { success = false, message = "Appointment was not found." });

            return Json(new { success = true, message = "Appointment updated." });
        }
        catch (AppointmentUpdateConflictException exception)
        {
            return Conflict(new
            {
                success = false,
                message = exception.IsCancelled
                    ? "Cancelled appointments cannot be edited."
                    : "The selected time conflicts with another appointment for this resource."
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to update a scheduling appointment.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { success = false, message = "The appointment could not be updated. Please try again." });
        }
    }

    [HttpPost("RescheduleAppointment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RescheduleAppointment(
        RescheduleAppointmentViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.AppointmentUid == Guid.Empty)
            ModelState.AddModelError(nameof(model.AppointmentUid), "Appointment identifier is required.");
        if (model.PrimaryResourceUid == Guid.Empty)
            ModelState.AddModelError(nameof(model.PrimaryResourceUid), "Primary resource is required.");
        if (model.StartDateTimeLocal == default)
            ModelState.AddModelError(nameof(model.StartDateTimeLocal), "Start time is required.");
        if (model.EndDateTimeLocal == default)
            ModelState.AddModelError(nameof(model.EndDateTimeLocal), "End time is required.");
        else if (model.EndDateTimeLocal <= model.StartDateTimeLocal)
            ModelState.AddModelError(nameof(model.EndDateTimeLocal), "End time must be after start time.");
        if (!ModelState.IsValid)
            return ValidationJson();

        try
        {
            var result = await _schedulingApiClient.RescheduleAppointmentAsync(
                model.AppointmentUid,
                new RescheduleAppointmentRequest
                {
                    PrimaryResourceUid = model.PrimaryResourceUid,
                    RoomResourceUid = model.RoomResourceUid,
                    StartDateTimeUtc = ToUtc(model.StartDateTimeLocal),
                    EndDateTimeUtc = ToUtc(model.EndDateTimeLocal)
                },
                cancellationToken);
            if (result is null)
                return NotFound(new { success = false, message = "Appointment was not found." });

            return Json(new { success = true, message = "Appointment rescheduled." });
        }
        catch (AppointmentUpdateConflictException exception)
        {
            return Conflict(new
            {
                success = false,
                message = exception.IsCancelled
                    ? "Cancelled appointments cannot be rescheduled."
                    : "The selected time conflicts with another appointment for this resource."
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to reschedule a scheduling appointment.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { success = false, message = "The appointment could not be rescheduled. Please try again." });
        }
    }

    private IActionResult ValidationJson() => BadRequest(new
    {
        success = false,
        message = "Please correct the highlighted errors.",
        errors = ModelState.Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "The value is invalid."
                    : error.ErrorMessage).ToArray())
    });

    private static DateTime ToUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();

    [HttpGet("AppointmentDetails")]
    public async Task<IActionResult> AppointmentDetails(
        Guid appointmentUid,
        CancellationToken cancellationToken)
    {
        if (appointmentUid == Guid.Empty)
            return BadRequest();

        try
        {
            var appointment = await _schedulingApiClient.GetAppointmentByUidAsync(
                appointmentUid, cancellationToken);
            if (appointment is null)
                return NotFound();

            return Json(new
            {
                appointment.AppointmentUid,
                appointment.PatientUid,
                appointment.PatientDisplayName,
                appointment.ChartNumber,
                appointment.PrimaryResourceUid,
                appointment.RoomResourceUid,
                appointment.PrimaryResourceName,
                appointment.RoomResourceName,
                startDateTimeLocal = NormalizeUtc(appointment.StartDateTimeUtc).ToLocalTime(),
                endDateTimeLocal = NormalizeUtc(appointment.EndDateTimeUtc).ToLocalTime(),
                appointment.AppointmentType,
                appointment.Reason,
                appointment.Notes,
                appointment.Status,
                appointment.CreatedByDisplayName,
                createdAtLocal = NormalizeUtc(appointment.CreatedAt).ToLocalTime()
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load scheduling appointment details.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Appointment details could not be loaded." });
        }
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        try
        {
            var model = new SchedulingIndexViewModel
            {
                Resources =
                    await _schedulingApiClient
                        .GetActiveResourcesAsync(cancellationToken)
            };

            return View(
                "~/Views/Scheduling/Index.cshtml",
                model);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to load scheduling resources because the API rejected the access token.");

            TempData["Error"] =
                "Scheduling resources could not be loaded because the API rejected the access token. Sign in again.";

            return View(
                "~/Views/Scheduling/Index.cshtml",
                new SchedulingIndexViewModel());
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to load scheduling resources from MicroEMR.Api.");

            TempData["Error"] =
                "Scheduling resources could not be loaded.";

            return View(
                "~/Views/Scheduling/Index.cshtml",
                new SchedulingIndexViewModel());
        }
    }

    [HttpGet("Events")]
    public async Task<IActionResult> Events(
        DateTime start,
        DateTime end,
        Guid? resourceUid,
        CancellationToken cancellationToken)
    {
        try
        {
            var appointments =
                await _schedulingApiClient.GetAppointmentsAsync(
                    NormalizeUtc(start),
                    NormalizeUtc(end),
                    resourceUid,
                    cancellationToken);

            var events =
                appointments.Select(appointment => new
                {
                    id = appointment.AppointmentUid,
                    text = BuildEventText(appointment),
                    start = FormatDayPilotLocal(
                        appointment.StartDateTimeUtc),
                    end = FormatDayPilotLocal(
                        appointment.EndDateTimeUtc),
                    resource = appointment.PrimaryResourceUid,
                    primaryResourceUid = appointment.PrimaryResourceUid
                });

            return Json(events);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to load scheduling events because the API rejected the access token.");

            return Unauthorized(new
            {
                message =
                    "The API rejected the access token. Sign in again."
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unable to load scheduling events from MicroEMR.Api.");

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    message =
                        "Scheduling events could not be loaded."
                });
        }
    }

    [HttpGet("MonthSummary")]
    public async Task<IActionResult> MonthSummary(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        if (start == default || end <= start || end - start > TimeSpan.FromDays(45))
            return BadRequest(new { message = "A valid month range of no more than 45 days is required." });

        try
        {
            var summary = await _schedulingApiClient.GetMonthSummaryAsync(
                NormalizeUtc(start), NormalizeUtc(end), cancellationToken);
            return Json(summary.Select(item => new
            {
                date = item.Date,
                item.AppointmentCount,
                item.ProviderCount,
                item.Status
            }));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "The API rejected the access token. Sign in again." });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load the scheduling month summary.");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "The month summary could not be loaded." });
        }
    }

    private static string BuildEventText(
        ScheduleAppointmentListItemResponse appointment)
    {
        var patientDisplayName =
            string.IsNullOrWhiteSpace(appointment.PatientDisplayName)
                ? "Patient"
                : appointment.PatientDisplayName.Trim();

        var secondaryText =
            !string.IsNullOrWhiteSpace(appointment.Reason)
                ? appointment.Reason.Trim()
                : appointment.AppointmentType?.Trim();

        return string.IsNullOrWhiteSpace(secondaryText)
            ? $"{patientDisplayName} - Appointment"
            : $"{patientDisplayName} - {secondaryText}";
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Local)
                .ToUniversalTime();
        }

        return value.ToUniversalTime();
    }

    private static string FormatDayPilotLocal(DateTime value)
    {
        return NormalizeUtc(value).ToLocalTime().ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fff",
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
