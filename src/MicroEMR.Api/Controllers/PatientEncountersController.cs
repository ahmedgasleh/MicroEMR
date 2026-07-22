using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.PatientEncounters;

namespace MicroEMR.Api.Controllers;

[ApiController]
// [Authorize]
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

    [AllowAnonymous] // For development only. Remove when API token validation is enabled consistently.
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

    [AllowAnonymous] // For development only. Remove when API token validation is enabled consistently.
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

    [Authorize]
    [HttpGet("api/patients/{patientUid:guid}/encounters/{encounterUid:guid}/history")]
    [ProducesResponseType<IReadOnlyList<PatientEncounterHistoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PatientEncounterHistoryResponse>>> GetEncounterHistory(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || encounterUid == Guid.Empty)
        {
            return BadRequest();
        }

        var history = await _encounterService.GetHistoryAsync(
            patientUid, encounterUid, cancellationToken);
        return Ok(history);
    }

    [Authorize]
    [HttpGet("api/patients/{patientUid:guid}/encounters/{encounterUid:guid}/addendums")]
    public async Task<ActionResult<IReadOnlyList<PatientEncounterAddendumResponse>>> GetEncounterAddendums(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || encounterUid == Guid.Empty)
            return BadRequest();

        var encounter = await _encounterService.GetByUidAsync(encounterUid, cancellationToken);
        if (encounter is null || encounter.PatientUid != patientUid)
            return NotFound(new { message = "Encounter was not found." });

        try
        {
            return Ok(await _encounterService.GetAddendumsAsync(
                patientUid, encounterUid, cancellationToken));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Failed to load addendums for encounter {EncounterUid}.", encounterUid);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Encounter addendums could not be loaded." });
        }
    }

    [Authorize]
    [HttpPost("api/patients/{patientUid:guid}/encounters/{encounterUid:guid}/addendums")]
    public async Task<ActionResult<PatientEncounterAddendumResponse>> CreateEncounterAddendum(
        Guid patientUid,
        Guid encounterUid,
        [FromBody] CreateEncounterAddendumRequest request,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || encounterUid == Guid.Empty)
            return BadRequest();
        if (string.IsNullOrWhiteSpace(request.AddendumText))
            return BadRequest(new { message = "Addendum text is required." });

        try
        {
            var addendum = await _encounterService.CreateAddendumAsync(
                patientUid, encounterUid, request, GetAuthenticatedUserId(), cancellationToken);
            return addendum is null
                ? NotFound(new { message = "Encounter was not found." })
                : Ok(addendum);
        }
        catch (EncounterAddendumNotAllowedException)
        {
            return Conflict(new { message = "Addendum can only be added to a signed encounter." });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Failed to add an addendum to encounter {EncounterUid}.", encounterUid);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Addendum could not be saved." });
        }
    }

    [AllowAnonymous] // For development only. Remove when API token validation is enabled consistently.
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

    [AllowAnonymous] // For development only. Remove when API token validation is enabled consistently.
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

    [Authorize]
    [HttpPut("api/patients/{patientUid:guid}/encounters/{encounterUid:guid}/soap-note")]
    [ProducesResponseType<PatientEncounterDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PatientEncounterDetailsResponse>> UpdateEncounterSoapNote(
        Guid patientUid,
        Guid encounterUid,
        [FromBody] UpdateEncounterSoapNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || encounterUid == Guid.Empty)
            return BadRequest();

        try
        {
            var encounter = await _encounterService.UpdateSoapNoteAsync(
                patientUid, encounterUid, request, GetAuthenticatedUserId(), cancellationToken);
            return encounter is null
                ? NotFound(new { message = "Encounter was not found." })
                : Ok(encounter);
        }
        catch (EncounterNoteNotEditableException)
        {
            return Conflict(new { message = "Signed encounter notes cannot be edited." });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Failed to update SOAP note for encounter {EncounterUid}.", encounterUid);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Encounter note could not be saved." });
        }
    }

    [AllowAnonymous] // For development only. Remove when API token validation is enabled consistently.
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
