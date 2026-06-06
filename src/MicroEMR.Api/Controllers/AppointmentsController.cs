using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.Scheduling.DTOs;
using MicroEMR.Application.Scheduling.Services;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;
    private readonly ILogger<AppointmentsController> _logger;

    public AppointmentsController(IAppointmentService appointmentService, ILogger<AppointmentsController> logger)
    {
        _appointmentService = appointmentService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> CreateAppointment([FromBody] CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var appointment = await _appointmentService.CreateAppointmentAsync(request, userId, cancellationToken);
            _logger.LogInformation("Appointment created for patient {PatientId} with provider {ProviderId}", request.PatientId, request.ProviderId);
            return CreatedAtAction(nameof(GetAppointment), new { id = appointment.Id }, appointment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating appointment");
            return BadRequest(new { message = "Error creating appointment", error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AppointmentDto>> GetAppointment(Guid id, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentService.GetAppointmentAsync(id, cancellationToken);
        if (appointment == null)
            return NotFound();
        return Ok(appointment);
    }

    [HttpPut("{id}/reschedule")]
    public async Task<ActionResult<AppointmentDto>> RescheduleAppointment(Guid id, [FromBody] RescheduleAppointmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            request.AppointmentId = id;
            var userId = GetCurrentUserId();
            var appointment = await _appointmentService.RescheduleAppointmentAsync(request, userId, cancellationToken);
            _logger.LogInformation("Appointment {AppointmentId} rescheduled", id);
            return Ok(appointment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rescheduling appointment");
            return BadRequest(new { message = "Error rescheduling appointment", error = ex.Message });
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelAppointment(Guid id, [FromBody] CancelAppointmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            request.AppointmentId = id;
            var userId = GetCurrentUserId();
            var result = await _appointmentService.CancelAppointmentAsync(request, userId, cancellationToken);
            _logger.LogInformation("Appointment {AppointmentId} cancelled", id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling appointment");
            return BadRequest(new { message = "Error cancelling appointment", error = ex.Message });
        }
    }

    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> ConfirmAppointment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _appointmentService.ConfirmAppointmentAsync(id, userId, cancellationToken);
            if (!result)
                return NotFound();
            _logger.LogInformation("Appointment {AppointmentId} confirmed", id);
            return Ok(new { message = "Appointment confirmed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming appointment");
            return BadRequest(new { message = "Error confirming appointment", error = ex.Message });
        }
    }

    [HttpGet("patient/{patientId}")]
    public async Task<ActionResult<List<AppointmentDto>>> GetPatientAppointments(Guid patientId, CancellationToken cancellationToken)
    {
        var appointments = await _appointmentService.GetPatientAppointmentsAsync(patientId, cancellationToken);
        return Ok(appointments);
    }

    [HttpGet("provider/{providerId}")]
    public async Task<ActionResult<List<AppointmentDto>>> GetProviderAppointments(Guid providerId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate, CancellationToken cancellationToken)
    {
        var appointments = await _appointmentService.GetProviderAppointmentsAsync(providerId, startDate, endDate, cancellationToken);
        return Ok(appointments);
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<List<AppointmentHistoryDto>>> GetAppointmentHistory(Guid id, CancellationToken cancellationToken)
    {
        var history = await _appointmentService.GetAppointmentHistoryAsync(id, cancellationToken);
        return Ok(history);
    }

    private Guid GetCurrentUserId()
    {
        // Extract from JWT or claims
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        return Guid.TryParse(userIdClaim?.Value, out var userId) ? userId : Guid.Empty;
    }
}
