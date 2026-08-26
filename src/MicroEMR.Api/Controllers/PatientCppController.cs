using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientCpp;
using MicroEMR.Application.SecurityAudit;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/patients/{patientUid:guid}/cpp")]
[RequirePermission(PermissionKeys.PatientsView)]
public sealed class PatientCppController(
    IPatientCppService service,
    ILogger<PatientCppController> logger) : ControllerBase
{
    [HttpGet]
    [SensitiveCapability(SecurityAuditCapabilities.PatientChartView)]
    [ProducesResponseType<PatientCppSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PatientCppSummaryResponse>> Get(
        Guid patientUid,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await service.GetAsync(patientUid, HttpContext.TraceIdentifier, cancellationToken);
            return summary is null ? NotFound() : Ok(summary);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "CPP aggregation failed before a safe summary could be returned. TraceIdentifier: {TraceIdentifier}.",
                HttpContext.TraceIdentifier);
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Patient summary unavailable",
                detail: "The patient summary cannot be opened because its required access boundary is unavailable.");
        }
    }
}

