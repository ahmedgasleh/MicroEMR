using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.Scheduling.DTOs;
using MicroEMR.Application.Scheduling.Services;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _calendarService;
    private readonly ILogger<CalendarController> _logger;

    public CalendarController(ICalendarService calendarService, ILogger<CalendarController> logger)
    {
        _calendarService = calendarService;
        _logger = logger;
    }

    [HttpGet("provider/{providerId}")]
    public async Task<ActionResult<CalendarViewDto>> GetProviderCalendar(
        Guid providerId,
        [FromQuery] Guid? clinicResourceId,
        [FromQuery] DateTime viewDate,
        CancellationToken cancellationToken)
    {
        var calendar = await _calendarService.GetCalendarViewAsync(providerId, clinicResourceId, viewDate, cancellationToken);
        return Ok(calendar);
    }

    [HttpPost("multi-provider")]
    public async Task<ActionResult<List<CalendarViewDto>>> GetMultiProviderCalendar(
        [FromBody] MultiProviderCalendarRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var calendars = await _calendarService.GetMultiProviderCalendarAsync(request.ProviderIds, request.ViewDate, cancellationToken);
            return Ok(calendars);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving multi-provider calendar");
            return BadRequest(new { message = "Error retrieving multi-provider calendar", error = ex.Message });
        }
    }

    [HttpGet("multi-resource/{providerId}")]
    public async Task<ActionResult<List<CalendarViewDto>>> GetMultiResourceCalendar(
        Guid providerId,
        [FromQuery] DateTime viewDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var calendars = await _calendarService.GetMultiResourceCalendarAsync(providerId, viewDate, cancellationToken);
            return Ok(calendars);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving multi-resource calendar");
            return BadRequest(new { message = "Error retrieving multi-resource calendar", error = ex.Message });
        }
    }

    [HttpPost("find-available-slots")]
    public async Task<ActionResult<List<ScheduleSlotDto>>> FindAvailableSlots(
        [FromBody] FindAvailableSlotsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var slots = await _calendarService.FindAvailableSlotsAsync(
                request.PatientId,
                request.ProviderIds,
                request.PreferredDate,
                request.DurationMinutes,
                cancellationToken);
            return Ok(slots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding available slots");
            return BadRequest(new { message = "Error finding available slots", error = ex.Message });
        }
    }
}

public class MultiProviderCalendarRequest
{
    public List<Guid> ProviderIds { get; set; } = new();
    public DateTime ViewDate { get; set; }
}

public class FindAvailableSlotsRequest
{
    public Guid PatientId { get; set; }
    public List<Guid> ProviderIds { get; set; } = new();
    public DateTime PreferredDate { get; set; }
    public int DurationMinutes { get; set; } = 15;
}
