using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientMedications.Contracts;
using MicroEMR.Application.PatientMedications.Services;
using MicroEMR.Application.PatientMedications;

namespace MicroEMR.Api.Controllers;

[ApiController]
// [Authorize]
[AllowAnonymous] // For development only. Remove this attribute when API token validation is enabled consistently.
public sealed class PatientMedicationsController : ControllerBase
{
    private readonly IPatientMedicationService _medicationService;
    private readonly ILogger<PatientMedicationsController> _logger;

    public PatientMedicationsController(
        IPatientMedicationService medicationService,
        ILogger<PatientMedicationsController> logger)
    {
        _medicationService = medicationService;
        _logger = logger;
    }

    [HttpGet("api/patients/{patientUid:guid}/medications")]
    [ProducesResponseType<IReadOnlyList<PatientMedicationListItemResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PatientMedicationListItemResponse>>>
        GetPatientMedications(
            Guid patientUid,
            CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        var medications =
            await _medicationService.GetByPatientUidAsync(
                patientUid,
                cancellationToken);

        return Ok(medications);
    }

    [HttpGet("api/patient-medications/{medicationUid:guid}")]
    [ProducesResponseType<PatientMedicationDetailsResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientMedicationDetailsResponse>>
        GetMedication(
            Guid medicationUid,
            CancellationToken cancellationToken)
    {
        if (medicationUid == Guid.Empty)
        {
            return BadRequest();
        }

        var medication =
            await _medicationService.GetByUidAsync(
                medicationUid,
                cancellationToken);

        if (medication is null)
        {
            return NotFound(new
            {
                message = "The requested medication was not found."
            });
        }

        return Ok(medication);
    }

    [HttpPost("api/patients/{patientUid:guid}/medications")]
    [ProducesResponseType<PatientMedicationDetailsResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientMedicationDetailsResponse>>
        CreateMedication(
            Guid patientUid,
            [FromBody] CreatePatientMedicationRequest request,
            CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(request.MedicationName))
        {
            ModelState.AddModelError(
                nameof(request.MedicationName),
                "Medication name is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var medication =
                await _medicationService.CreateAsync(
                    patientUid,
                    request,
                    GetAuthenticatedUserId(),
                    GetAuthenticatedDisplayName(),
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetMedication),
                new
                {
                    medicationUid = medication.MedicationUid
                },
                medication);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to create medication for patient {PatientUid}.",
                patientUid);

            return BadRequest(new
            {
                message = exception.Message
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to create medication for patient {PatientUid}.",
                patientUid);

            throw;
        }
    }

    [HttpPut("api/patients/{patientUid:guid}/medications/{medicationUid:guid}")]
    public async Task<ActionResult<PatientMedicationDetailsResponse>> UpdateMedication(
        Guid patientUid, Guid medicationUid,
        [FromBody] UpdatePatientMedicationRequest request,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || medicationUid == Guid.Empty) return BadRequest();
        request.MedicationName = request.MedicationName?.Trim() ?? string.Empty;
        request.Status = request.Status?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(request.MedicationName))
            ModelState.AddModelError(nameof(request.MedicationName), "Medication name is required.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var result = await _medicationService.UpdateAsync(
                patientUid, medicationUid, request, GetAuthenticatedUserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (FormatException) { return BadRequest(new { message = "The row version is invalid." }); }
        catch (PatientMedicationConcurrencyException)
        { return Conflict(new { message = "The medication was changed by another user. Reload and try again." }); }
    }

    [HttpPost("api/patients/{patientUid:guid}/medications/{medicationUid:guid}/discontinue")]
    public async Task<ActionResult<PatientMedicationDetailsResponse>> DiscontinueMedication(
        Guid patientUid, Guid medicationUid,
        [FromBody] DiscontinuePatientMedicationRequest request,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || medicationUid == Guid.Empty) return BadRequest();
        request.DiscontinueReason = request.DiscontinueReason?.Trim();
        if (request.DiscontinueReason?.Length > 500)
            return BadRequest(new { message = "Discontinue reason cannot exceed 500 characters." });
        var result = await _medicationService.DiscontinueAsync(
            patientUid, medicationUid, request, GetAuthenticatedUserId(), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    private long? GetAuthenticatedUserId()
    {
        var userIdValue =
            User.FindFirstValue("user_id")
            ?? User.FindFirstValue("userid")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return long.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }

    private string? GetAuthenticatedDisplayName()
    {
        return User.FindFirstValue("name")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.Identity?.Name;
    }
}
