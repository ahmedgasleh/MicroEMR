using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.PlatformEntitlements;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Application.TenantUserAdministration;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Route("api/platform/security-audit")]
[RequirePlatformEntitlement(PlatformEntitlementKeys.SecurityAuditView)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlatformSecurityAuditController(
    IPlatformSecurityAuditReviewService service,
    IAuthenticatedSubjectAccessor subjectAccessor,
    ILogger<PlatformSecurityAuditController> logger) : ControllerBase
{
    [HttpPost("search")]
    [ProducesResponseType<SecurityAuditSearchPage>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Search(
        [FromBody] SecurityAuditSearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.SearchAsync(
                request, subjectAccessor.GetRequiredSubject(), Guid.NewGuid(), cancellationToken);
            return Ok(result);
        }
        catch (SecurityAuditReviewValidationException exception)
        {
            ModelState.AddModelError("search", exception.Message);
            return ValidationProblem(ModelState);
        }
        catch (SecurityAuditDisclosureUnavailableException exception)
        {
            logger.LogError(exception,
                "Security audit search disclosure failed closed. TraceIdentifier: {TraceIdentifier}.",
                HttpContext.TraceIdentifier);
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: "Security audit review is temporarily unavailable.");
        }
    }

    [HttpGet("events/{securityAuditEventUid:guid}")]
    [ProducesResponseType<SecurityAuditDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(
        Guid securityAuditEventUid, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.GetByUidAsync(
                securityAuditEventUid, subjectAccessor.GetRequiredSubject(), Guid.NewGuid(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (SecurityAuditReviewValidationException exception)
        {
            ModelState.AddModelError(nameof(securityAuditEventUid), exception.Message);
            return ValidationProblem(ModelState);
        }
        catch (SecurityAuditDisclosureUnavailableException exception)
        {
            logger.LogError(exception,
                "Security audit detail disclosure failed closed. TraceIdentifier: {TraceIdentifier}.",
                HttpContext.TraceIdentifier);
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: "Security audit review is temporarily unavailable.");
        }
    }
}
