using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Scheduling.DTOs;
using MicroEMR.Application.Scheduling.Services;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize, RequirePermission(PermissionKeys.SchedulingManage)]
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
            _logger.LogInformation("Generated {SlotCount} scheduling slots.", slots.Count);
            return Ok(slots);
        }
        catch (Exception exception) when (SchedulingExceptionResponses.IsExpected(exception))
        {
            return SchedulingExceptionResponses.ToResult(exception);
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
            _logger.LogInformation("Scheduling slot blocked.");
            return Ok(new { message = "Slot blocked" });
        }
        catch (Exception exception) when (SchedulingExceptionResponses.IsExpected(exception))
        {
            return SchedulingExceptionResponses.ToResult(exception);
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
            _logger.LogInformation("Scheduling slot unblocked.");
            return Ok(new { message = "Slot unblocked" });
        }
        catch (Exception exception) when (SchedulingExceptionResponses.IsExpected(exception))
        {
            return SchedulingExceptionResponses.ToResult(exception);
        }
    }
}

public class BlockSlotRequest
{
    public string Reason { get; set; } = string.Empty;
}
