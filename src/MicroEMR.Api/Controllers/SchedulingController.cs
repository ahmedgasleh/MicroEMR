using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MicroEMR.Application.Scheduling;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Services;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/scheduling")]
public sealed class SchedulingController : ControllerBase
{
    private static readonly HashSet<string> AllowedAppointmentStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Scheduled", "Arrived", "Roomed", "Seen", "Completed"
        };
    private readonly ISchedulingReadService _schedulingReadService;
    private readonly ISchedulingAppointmentService _schedulingAppointmentService;
    private readonly IPatientEncounterService _patientEncounterService;

    public SchedulingController(
        ISchedulingReadService schedulingReadService,
        ISchedulingAppointmentService schedulingAppointmentService,
        IPatientEncounterService patientEncounterService)
    {
        _schedulingReadService = schedulingReadService;
        _schedulingAppointmentService = schedulingAppointmentService;
        _patientEncounterService = patientEncounterService;
    }

    [HttpPost("appointments")]
    [ProducesResponseType(typeof(ScheduleAppointmentListItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScheduleAppointmentListItemResponse>> CreateAppointment(
        [FromBody] CreateScheduleAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PatientUid == Guid.Empty)
            ModelState.AddModelError(nameof(request.PatientUid), "Patient is required.");
        if (request.PrimaryResourceUid == Guid.Empty)
            ModelState.AddModelError(nameof(request.PrimaryResourceUid), "Primary resource is required.");
        if (request.StartDateTimeUtc == default)
            ModelState.AddModelError(nameof(request.StartDateTimeUtc), "Start time is required.");
        if (request.EndDateTimeUtc == default)
            ModelState.AddModelError(nameof(request.EndDateTimeUtc), "End time is required.");
        else if (request.EndDateTimeUtc <= request.StartDateTimeUtc)
            ModelState.AddModelError(nameof(request.EndDateTimeUtc), "End time must be after start time.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var appointment = await _schedulingAppointmentService.CreateAsync(
                request, GetAuthenticatedUserId(), cancellationToken);
            return StatusCode(StatusCodes.Status201Created, appointment);
        }
        catch (SchedulingConflictException)
        {
            return Conflict(new { message = "The selected time conflicts with another appointment for this resource." });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private long? GetAuthenticatedUserId()
    {
        var value = User.FindFirstValue("user_id")
            ?? User.FindFirstValue("userid")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var userId) ? userId : null;
    }

    [HttpGet("resources")]
    [ProducesResponseType(
        typeof(IReadOnlyList<ScheduleResourceResponse>),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ScheduleResourceResponse>>>
        GetActiveResources(CancellationToken cancellationToken = default)
    {
        var resources =
            await _schedulingReadService.GetActiveResourcesAsync(
                cancellationToken);

        return Ok(resources);
    }

    [HttpGet("appointments")]
    [ProducesResponseType(
        typeof(IReadOnlyList<ScheduleAppointmentListItemResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ScheduleAppointmentListItemResponse>>>
        GetAppointments(
            [FromQuery] DateTime startUtc,
            [FromQuery] DateTime endUtc,
            [FromQuery] Guid? resourceUid,
            CancellationToken cancellationToken = default)
    {
        if (endUtc <= startUtc)
        {
            return BadRequest(new
            {
                message = "endUtc must be after startUtc."
            });
        }

        var appointments =
            await _schedulingReadService.GetAppointmentsAsync(
                startUtc,
                endUtc,
                resourceUid,
                cancellationToken);

        return Ok(appointments);
    }

    [HttpGet("appointments/{appointmentUid:guid}")]
    [ProducesResponseType(typeof(ScheduleAppointmentDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScheduleAppointmentDetailsResponse>> GetAppointment(
        Guid appointmentUid,
        CancellationToken cancellationToken = default)
    {
        if (appointmentUid == Guid.Empty)
            return BadRequest();

        var appointment = await _schedulingReadService.GetAppointmentByUidAsync(
            appointmentUid, cancellationToken);
        return appointment is null ? NotFound() : Ok(appointment);
    }

    [HttpGet("month-summary")]
    [ProducesResponseType(typeof(IReadOnlyList<ScheduleMonthSummaryItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ScheduleMonthSummaryItemResponse>>> GetMonthSummary(
        [FromQuery] DateTime startUtc,
        [FromQuery] DateTime endUtc,
        CancellationToken cancellationToken = default)
    {
        if (startUtc == default || endUtc <= startUtc || endUtc - startUtc > TimeSpan.FromDays(45))
            return BadRequest(new { message = "A valid month summary range of no more than 45 days is required." });

        var summary = await _schedulingReadService.GetMonthSummaryAsync(
            startUtc, endUtc, cancellationToken);
        return Ok(summary);
    }

    [HttpPost("appointments/{appointmentUid:guid}/cancel")]
    [ProducesResponseType(typeof(CancelScheduleAppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CancelScheduleAppointmentResponse>> CancelAppointment(
        Guid appointmentUid,
        [FromBody] CancelScheduleAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (appointmentUid == Guid.Empty)
            return BadRequest(new { message = "Appointment identifier is required." });
        request.CancelReason = request.CancelReason?.Trim();
        if (request.CancelReason?.Length > 500)
            return BadRequest(new { message = "Cancel reason cannot exceed 500 characters." });

        try
        {
            var result = await _schedulingAppointmentService.CancelAsync(
                appointmentUid, request, GetAuthenticatedUserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (AppointmentAlreadyCancelledException)
        {
            return Conflict(new { message = "The appointment is already cancelled." });
        }
    }

    [HttpPost("appointments/{appointmentUid:guid}/start-encounter")]
    [ProducesResponseType(typeof(StartEncounterFromAppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StartEncounterFromAppointmentResponse>> StartEncounterFromAppointment(
        Guid appointmentUid,
        CancellationToken cancellationToken = default)
    {
        if (appointmentUid == Guid.Empty)
            return BadRequest(new { message = "Appointment identifier is required." });

        try
        {
            var result = await _patientEncounterService.StartFromAppointmentAsync(
                appointmentUid, GetAuthenticatedUserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (AppointmentCancelledException)
        {
            return Conflict(new
            {
                code = "appointment_cancelled",
                message = "Cancelled appointments cannot start encounters."
            });
        }
        catch (AppointmentCompletedException)
        {
            return Conflict(new
            {
                code = "appointment_completed",
                message = "Completed appointments cannot start a new encounter."
            });
        }
    }

    [HttpPost("appointments/{appointmentUid:guid}/status")]
    [ProducesResponseType(typeof(UpdateAppointmentStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UpdateAppointmentStatusResponse>> UpdateAppointmentStatus(
        Guid appointmentUid,
        [FromBody] UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (appointmentUid == Guid.Empty)
            return BadRequest(new { message = "Appointment identifier is required." });
        if (!AllowedAppointmentStatuses.Contains(request.Status))
            return BadRequest(new { message = "Invalid appointment status." });

        try
        {
            var result = await _schedulingAppointmentService.UpdateStatusAsync(
                appointmentUid, request, GetAuthenticatedUserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (AppointmentAlreadyCancelledException)
        {
            return Conflict(new
            {
                code = "appointment_cancelled",
                message = "Cancelled appointments cannot be updated."
            });
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "Invalid appointment status." });
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { message = "Invalid appointment status." });
        }
    }

    [HttpPut("appointments/{appointmentUid:guid}")]
    [ProducesResponseType(typeof(ScheduleAppointmentDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScheduleAppointmentDetailsResponse>> UpdateAppointment(
        Guid appointmentUid,
        [FromBody] UpdateScheduleAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (appointmentUid == Guid.Empty)
            ModelState.AddModelError(nameof(appointmentUid), "Appointment identifier is required.");
        if (request.PrimaryResourceUid == Guid.Empty)
            ModelState.AddModelError(nameof(request.PrimaryResourceUid), "Primary resource is required.");
        if (request.EndDateTimeUtc <= request.StartDateTimeUtc)
            ModelState.AddModelError(nameof(request.EndDateTimeUtc), "End time must be after start time.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var result = await _schedulingAppointmentService.UpdateAsync(
                appointmentUid, request, GetAuthenticatedUserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (SchedulingConflictException)
        {
            return Conflict(new { code = "appointment_conflict", message = "The selected time conflicts with another appointment for this resource." });
        }
        catch (AppointmentAlreadyCancelledException)
        {
            return Conflict(new { code = "appointment_cancelled", message = "Cancelled appointments cannot be edited." });
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { message = "The appointment update request is invalid." });
        }
    }

    [HttpPost("appointments/{appointmentUid:guid}/reschedule")]
    [ProducesResponseType(typeof(ScheduleAppointmentDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScheduleAppointmentDetailsResponse>> RescheduleAppointment(
        Guid appointmentUid,
        [FromBody] RescheduleAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (appointmentUid == Guid.Empty)
            ModelState.AddModelError(nameof(appointmentUid), "Appointment identifier is required.");
        if (request.PrimaryResourceUid == Guid.Empty)
            ModelState.AddModelError(nameof(request.PrimaryResourceUid), "Primary resource is required.");
        if (request.StartDateTimeUtc == default)
            ModelState.AddModelError(nameof(request.StartDateTimeUtc), "Start time is required.");
        if (request.EndDateTimeUtc == default)
            ModelState.AddModelError(nameof(request.EndDateTimeUtc), "End time is required.");
        else if (request.EndDateTimeUtc <= request.StartDateTimeUtc)
            ModelState.AddModelError(nameof(request.EndDateTimeUtc), "End time must be after start time.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var result = await _schedulingAppointmentService.RescheduleAsync(
                appointmentUid, request, GetAuthenticatedUserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (SchedulingConflictException)
        {
            return Conflict(new { code = "appointment_conflict", message = "The selected time conflicts with another appointment for this resource." });
        }
        catch (AppointmentAlreadyCancelledException)
        {
            return Conflict(new { code = "appointment_cancelled", message = "Cancelled appointments cannot be rescheduled." });
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { message = "The appointment reschedule request is invalid." });
        }
    }
}
