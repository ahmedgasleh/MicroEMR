using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Reflection.Emit;
using System.Security.Claims;
using System.Security.Cryptography.Xml;

using MicroEMR.Application.Patients.Contracts;
using MicroEMR.Application.Patients.Exceptions;
using MicroEMR.Application.Patients.Services;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.ReadAudit;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Route("api/patients")]
[Authorize]
public sealed class PatientsController : ControllerBase
{
    private readonly IPatientService _patientService;
    private readonly ILogger<PatientsController> _logger;
    private readonly IPatientChartReadAuditService _readAudit;

    public PatientsController (
        IPatientService patientService,
        ILogger<PatientsController> logger,
        IPatientChartReadAuditService readAudit )
    {
        _patientService = patientService;
        _logger = logger;
        _readAudit = readAudit;
    }

    [HttpPost("{patientUid:guid}/chart-open")]
    [RequirePermission(PermissionKeys.PatientsView)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RecordChartOpened(
        Guid patientUid,
        CancellationToken cancellationToken = default)
    {
        var patient = await _patientService.GetByUidAsync(patientUid, cancellationToken);
        if (patient is null) return NotFound();

        try
        {
            await _readAudit.RecordOpenedAsync(
                patient.PatientUid, HttpContext.TraceIdentifier, cancellationToken);
            return NoContent();
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Patient chart access audit failed for patient {PatientUid}; chart access was prevented. TraceIdentifier: {TraceIdentifier}.",
                patient.PatientUid, HttpContext.TraceIdentifier);
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Patient chart audit unavailable",
                detail: "The patient chart cannot be opened because access auditing is temporarily unavailable.");
        }
    }

    [HttpGet]
    [RequirePermission(PermissionKeys.PatientsView)]
    [ProducesResponseType(
        typeof(PatientSearchResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PatientSearchResponse>> Search (
        [FromQuery] string? searchText,
        [FromQuery] DateOnly? dateOfBirth,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default )
    {
        var result =
            await _patientService.SearchAsync(
                searchText,
                dateOfBirth,
                pageNumber,
                pageSize,
                includeInactive,
                cancellationToken);

        return Ok(result);
    }

    [HttpGet("{patientUid:guid}")]
    [RequirePermission(PermissionKeys.PatientsView)]
    [ProducesResponseType(
        typeof(PatientDetailsResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PatientDetailsResponse>> GetByUid (
        Guid patientUid,
        CancellationToken cancellationToken = default )
    {
        var patient =
            await _patientService.GetByUidAsync(
                patientUid,
                cancellationToken);

        if (patient is null)
        {
            return NotFound(
                new
                {
                    message = "Patient was not found."
                });
        }

        return Ok(patient);
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.PatientsEdit)]
    [ProducesResponseType(
        typeof(PatientDetailsResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PatientDetailsResponse>> Create (
        [FromBody] CreatePatientRequest request,
        CancellationToken cancellationToken = default )
    {
        var patient =
            await _patientService.CreateAsync(
                request,
                ClinicalUserActorContext.GetRequired(HttpContext),
                cancellationToken);

        return CreatedAtAction(
            nameof(GetByUid),
            new
            {
                patientUid = patient.PatientUid
            },
            patient);
    }

    [HttpPut("{patientUid:guid}")]
    [RequirePermission(PermissionKeys.PatientsEdit)]
    [ProducesResponseType(
        typeof(PatientDetailsResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PatientDetailsResponse>>
        UpdateDemographics(
            Guid patientUid,
            [FromBody] UpdatePatientDemographicsRequest request,
            CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var patient =
                await _patientService.UpdateDemographicsAsync(
                    patientUid,
                    request,
                    GetAuthenticatedUserId(),
                    cancellationToken);

            if (patient is null)
            {
                return NotFound(new
                {
                    message = "Patient was not found."
                });
            }

            return Ok(patient);
        }
        catch (PatientDemographicsConcurrencyException exception)
        {
            _logger.LogWarning(
                exception,
                "Concurrency conflict while updating patient {PatientUid}.",
                patientUid);

            return Conflict(new
            {
                message = exception.Message
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to update demographics for patient {PatientUid}.",
                patientUid);

            throw;
        }
    }

    private long GetAuthenticatedUserId() =>
        ClinicalUserActorContext.GetRequired(HttpContext);

}
