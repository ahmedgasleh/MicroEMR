using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientAllergies.Contracts;
using MicroEMR.Application.PatientAllergies.Services;
using MicroEMR.Application.PatientAllergies;

namespace MicroEMR.Api.Controllers;

[ApiController]
// [Authorize]
[AllowAnonymous] // For development only. Remove this attribute when API token validation is enabled consistently.
public sealed class PatientAllergiesController : ControllerBase
{
    private readonly IPatientAllergyService _allergyService;
    private readonly ILogger<PatientAllergiesController> _logger;

    public PatientAllergiesController(
        IPatientAllergyService allergyService,
        ILogger<PatientAllergiesController> logger)
    {
        _allergyService = allergyService;
        _logger = logger;
    }

    [HttpGet("api/patients/{patientUid:guid}/allergies")]
    [ProducesResponseType<IReadOnlyList<PatientAllergyListItemResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PatientAllergyListItemResponse>>>
        GetPatientAllergies(
            Guid patientUid,
            CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        var allergies =
            await _allergyService.GetByPatientUidAsync(
                patientUid,
                cancellationToken);

        return Ok(allergies);
    }

    [HttpGet("api/patient-allergies/{allergyUid:guid}")]
    [ProducesResponseType<PatientAllergyDetailsResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientAllergyDetailsResponse>>
        GetAllergy(
            Guid allergyUid,
            CancellationToken cancellationToken)
    {
        if (allergyUid == Guid.Empty)
        {
            return BadRequest();
        }

        var allergy =
            await _allergyService.GetByUidAsync(
                allergyUid,
                cancellationToken);

        if (allergy is null)
        {
            return NotFound(new
            {
                message = "The requested allergy was not found."
            });
        }

        return Ok(allergy);
    }

    [HttpPost("api/patients/{patientUid:guid}/allergies")]
    [ProducesResponseType<PatientAllergyDetailsResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientAllergyDetailsResponse>>
        CreateAllergy(
            Guid patientUid,
            [FromBody] CreatePatientAllergyRequest request,
            CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(request.AllergenName))
        {
            ModelState.AddModelError(
                nameof(request.AllergenName),
                "Allergen name is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var allergy =
                await _allergyService.CreateAsync(
                    patientUid,
                    request,
                    GetAuthenticatedUserId(),
                    GetAuthenticatedDisplayName(),
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetAllergy),
                new
                {
                    allergyUid = allergy.AllergyUid
                },
                allergy);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to create allergy for patient {PatientUid}.",
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
                "Failed to create allergy for patient {PatientUid}.",
                patientUid);

            throw;
        }
    }

    [HttpPut("api/patients/{patientUid:guid}/allergies/{allergyUid:guid}")]
    [ProducesResponseType<PatientAllergyDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PatientAllergyDetailsResponse>> UpdateAllergy(
        Guid patientUid,
        Guid allergyUid,
        [FromBody] UpdatePatientAllergyRequest request,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || allergyUid == Guid.Empty)
            return BadRequest();

        request.AllergenName = request.AllergenName?.Trim() ?? string.Empty;
        request.AllergenType = request.AllergenType?.Trim();
        request.Reaction = request.Reaction?.Trim();
        request.Severity = request.Severity?.Trim();
        request.Status = request.Status?.Trim() ?? string.Empty;
        request.Notes = request.Notes?.Trim();

        if (string.IsNullOrWhiteSpace(request.AllergenName))
            ModelState.AddModelError(nameof(request.AllergenName), "Allergen name is required.");
        if (string.IsNullOrWhiteSpace(request.Status))
            ModelState.AddModelError(nameof(request.Status), "Status is required.");
        if (string.IsNullOrWhiteSpace(request.RowVersion))
            ModelState.AddModelError(nameof(request.RowVersion), "Row version is required.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var allergy = await _allergyService.UpdateAsync(
                patientUid, allergyUid, request, GetAuthenticatedUserId(), cancellationToken);
            return allergy is null ? NotFound() : Ok(allergy);
        }
        catch (FormatException)
        {
            return BadRequest(new { message = "The row version is invalid." });
        }
        catch (PatientAllergyConcurrencyException)
        {
            return Conflict(new { message = "The allergy was changed by another user. Reload and try again." });
        }
    }

    [HttpPost("api/patients/{patientUid:guid}/allergies/{allergyUid:guid}/resolve")]
    public async Task<ActionResult<PatientAllergyDetailsResponse>> ResolveAllergy(
        Guid patientUid, Guid allergyUid, [FromBody] ResolvePatientAllergyRequest request,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || allergyUid == Guid.Empty) return BadRequest();
        request.ResolveReason = request.ResolveReason?.Trim();
        if (request.ResolveReason?.Length > 500) return BadRequest(new { message = "Resolve reason cannot exceed 500 characters." });
        var result = await _allergyService.ResolveAsync(patientUid, allergyUid, request, GetAuthenticatedUserId(), cancellationToken);
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
