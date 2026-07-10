using MicroEMR.Application.Scheduling.Contracts;
using MicroEMR.Application.Scheduling.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MicroEMR.Application.Scheduling;

namespace MicroEMR.Api.Controllers;

[ApiController]
[AllowAnonymous] // Development compatibility until API token validation is enabled consistently.
[Route("api/scheduling")]
public sealed class SchedulingController : ControllerBase
{
    private readonly ISchedulingReadService _schedulingReadService;
    private readonly ISchedulingAppointmentService _schedulingAppointmentService;

    public SchedulingController(
        ISchedulingReadService schedulingReadService,
        ISchedulingAppointmentService schedulingAppointmentService)
    {
        _schedulingReadService = schedulingReadService;
        _schedulingAppointmentService = schedulingAppointmentService;
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
        if (request.EndDateTimeUtc <= request.StartDateTimeUtc)
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
}
