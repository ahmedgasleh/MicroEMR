using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.Scheduling.DTOs;
using MicroEMR.Application.Scheduling.Services;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScheduleSlotsController : ControllerBase
{
    private readonly IScheduleSlotService _scheduleSlotService;
    private readonly ILogger<ScheduleSlotsController> _logger;

    public ScheduleSlotsController(IScheduleSlotService scheduleSlotService, ILogger<ScheduleSlotsController> logger)
    {
        _scheduleSlotService = scheduleSlotService;
        _logger = logger;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<List<ScheduleSlotDto>>> GenerateSlots([FromBody] GenerateScheduleSlotsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var slots = await _scheduleSlotService.GenerateSlotsAsync(request, cancellationToken);
            _logger.LogInformation("Generated {SlotCount} slots for provider {ProviderId}", slots.Count, request.ProviderId);
            return Ok(slots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating schedule slots");
            return BadRequest(new { message = "Error generating schedule slots", error = ex.Message });
        }
    }

    [HttpGet("available")]
    public async Task<ActionResult<List<ScheduleSlotDto>>> GetAvailableSlots(
        [FromQuery] Guid providerId,
        [FromQuery] Guid? clinicResourceId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        var slots = await _scheduleSlotService.GetAvailableSlotsAsync(providerId, clinicResourceId, startDate, endDate, cancellationToken);
        return Ok(slots);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ScheduleSlotDto>> GetSlot(Guid id, CancellationToken cancellationToken)
    {
        var slot = await _scheduleSlotService.GetSlotAsync(id, cancellationToken);
        if (slot == null)
            return NotFound();
        return Ok(slot);
    }

    [HttpPost("{id}/block")]
    public async Task<IActionResult> BlockSlot(Guid id, [FromBody] BlockSlotRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _scheduleSlotService.BlockSlotAsync(id, request.Reason, cancellationToken);
            if (!result)
                return NotFound();
            _logger.LogInformation("Slot {SlotId} blocked", id);
            return Ok(new { message = "Slot blocked" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blocking slot");
            return BadRequest(new { message = "Error blocking slot", error = ex.Message });
        }
    }

    [HttpPost("{id}/unblock")]
    public async Task<IActionResult> UnblockSlot(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _scheduleSlotService.UnblockSlotAsync(id, cancellationToken);
            if (!result)
                return NotFound();
            _logger.LogInformation("Slot {SlotId} unblocked", id);
            return Ok(new { message = "Slot unblocked" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unblocking slot");
            return BadRequest(new { message = "Error unblocking slot", error = ex.Message });
        }
    }
}

public class BlockSlotRequest
{
    public string Reason { get; set; } = string.Empty;
}
