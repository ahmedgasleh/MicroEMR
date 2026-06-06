using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.Scheduling.DTOs;
using MicroEMR.Application.Scheduling.Services;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResourceBlocksController : ControllerBase
{
    private readonly IResourceBlockService _resourceBlockService;
    private readonly ILogger<ResourceBlocksController> _logger;

    public ResourceBlocksController(IResourceBlockService resourceBlockService, ILogger<ResourceBlocksController> logger)
    {
        _resourceBlockService = resourceBlockService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ResourceBlockDto>> CreateBlock([FromBody] CreateResourceBlockRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var block = await _resourceBlockService.CreateBlockAsync(request, userId, cancellationToken);
            _logger.LogInformation("Resource block created for resource {ResourceId}", request.ResourceId);
            return CreatedAtAction(nameof(GetBlock), new { id = block.Id }, block);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating resource block");
            return BadRequest(new { message = "Error creating resource block", error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ResourceBlockDto>> GetBlock(Guid id, CancellationToken cancellationToken)
    {
        var block = await _resourceBlockService.GetBlockAsync(id, cancellationToken);
        if (block == null)
            return NotFound();
        return Ok(block);
    }

    [HttpGet("provider/{providerId}")]
    public async Task<ActionResult<List<ResourceBlockDto>>> GetProviderBlocks(
        Guid providerId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        var blocks = await _resourceBlockService.GetProviderBlocksAsync(providerId, startDate, endDate, cancellationToken);
        return Ok(blocks);
    }

    [HttpGet("resource/{resourceId}")]
    public async Task<ActionResult<List<ResourceBlockDto>>> GetResourceBlocks(
        Guid resourceId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        var blocks = await _resourceBlockService.GetResourceBlocksAsync(resourceId, startDate, endDate, cancellationToken);
        return Ok(blocks);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBlock(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _resourceBlockService.DeleteBlockAsync(id, userId, cancellationToken);
            if (!result)
                return NotFound();
            _logger.LogInformation("Resource block {BlockId} deleted", id);
            return Ok(new { message = "Resource block deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting resource block");
            return BadRequest(new { message = "Error deleting resource block", error = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        return Guid.TryParse(userIdClaim?.Value, out var userId) ? userId : Guid.Empty;
    }
}
