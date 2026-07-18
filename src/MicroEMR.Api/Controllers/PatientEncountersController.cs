using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.PatientEncounters;

namespace MicroEMR.Api.Controllers;

[ApiController]
// [Authorize]
[AllowAnonymous] // For development only. Remove this attribute when API token validation is enabled consistently.
public sealed class PatientEncountersController : ControllerBase
{
    private readonly IPatientEncounterService _encounterService;
    private readonly ILogger<PatientEncountersController> _logger;

    public PatientEncountersController(
        IPatientEncounterService encounterService,
        ILogger<PatientEncountersController> logger)
    {
        _encounterService = encounterService;
        _logger = logger;
    }

    [HttpGet("api/patients/{patientUid:guid}/encounters")]
    [ProducesResponseType<IReadOnlyList<PatientEncounterListItemResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PatientEncounterListItemResponse>>>
        GetPatientEncounters(
            Guid patientUid,
            CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        var encounters =
            await _encounterService.GetByPatientUidAsync(
                patientUid,
                cancellationToken);

        return Ok(encounters);
    }

    [HttpGet("api/patient-encounters/{encounterUid:guid}")]
    [ProducesResponseType<PatientEncounterDetailsResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientEncounterDetailsResponse>>
        GetEncounter(
            Guid encounterUid,
            CancellationToken cancellationToken)
    {
        if (encounterUid == Guid.Empty)
        {
            return BadRequest();
        }

        var encounter =
            await _encounterService.GetByUidAsync(
                encounterUid,
                cancellationToken);

        if (encounter is null)
        {
            return NotFound(new
            {
                message = "The requested encounter was not found."
            });
        }

        return Ok(encounter);
    }

    [HttpPost("api/patients/{patientUid:guid}/encounters")]
    [ProducesResponseType<PatientEncounterDetailsResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientEncounterDetailsResponse>>
        CreateEncounter(
            Guid patientUid,
            [FromBody] CreatePatientEncounterRequest request,
            CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        if (request.EncounterDateUtc == default)
        {
            ModelState.AddModelError(
                nameof(request.EncounterDateUtc),
                "Encounter date/time is required.");
        }

        if (string.IsNullOrWhiteSpace(request.EncounterType))
        {
            ModelState.AddModelError(
                nameof(request.EncounterType),
                "Encounter type is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var encounter =
                await _encounterService.CreateAsync(
                    patientUid,
                    request,
                    GetAuthenticatedUserId(),
                    GetAuthenticatedDisplayName(),
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetEncounter),
                new
                {
                    encounterUid = encounter.EncounterUid
                },
                encounter);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to create encounter for patient {PatientUid}.",
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
                "Failed to create encounter for patient {PatientUid}.",
                patientUid);

            throw;
        }
    }

    [HttpPut("api/patients/{patientUid:guid}/encounters/{encounterUid:guid}/note")]
    [ProducesResponseType<PatientEncounterDetailsResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PatientEncounterDetailsResponse>>
        UpdateEncounterNote(
            Guid patientUid,
            Guid encounterUid,
            [FromBody] UpdateEncounterNoteRequest request,
            CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || encounterUid == Guid.Empty)
        {
            return BadRequest();
        }

        try
        {
            var encounter = await _encounterService.UpdateNoteAsync(
                patientUid,
                encounterUid,
                request,
                GetAuthenticatedUserId(),
                cancellationToken);

            return encounter is null
                ? NotFound(new { message = "Encounter was not found." })
                : Ok(encounter);
        }
        catch (EncounterNoteNotEditableException)
        {
            return Conflict(new
            {
                message = "Encounter note cannot be edited."
            });
        }
    }

    [HttpPost("api/patients/{patientUid:guid}/encounters/{encounterUid:guid}/sign")]
    [ProducesResponseType<PatientEncounterDetailsResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PatientEncounterDetailsResponse>>
        SignEncounter(
            Guid patientUid,
            Guid encounterUid,
            CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || encounterUid == Guid.Empty)
        {
            return BadRequest();
        }

        try
        {
            var encounter = await _encounterService.SignAsync(
                patientUid,
                encounterUid,
                GetAuthenticatedUserId(),
                cancellationToken);

            return encounter is null
                ? NotFound(new { message = "Encounter was not found." })
                : Ok(encounter);
        }
        catch (EncounterCannotBeSignedException)
        {
            return Conflict(new
            {
                message = "Encounter cannot be signed."
            });
        }
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
