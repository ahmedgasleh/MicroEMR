using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientEncounters.Contracts;
using MicroEMR.Application.PatientEncounters.Services;
using MicroEMR.Application.PatientEncounters;
using MicroEMR.Application.ClinicalOutput;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission(PermissionKeys.EncountersView)]
public sealed class PatientEncountersController : ControllerBase
{
    private readonly IPatientEncounterService _encounterService;
    private readonly ILogger<PatientEncountersController> _logger;
    private readonly IClinicalPdfPreviewService _pdfPreview;
    private readonly IClinicalArtifactService _artifacts;

    public PatientEncountersController(
        IPatientEncounterService encounterService,
        ILogger<PatientEncountersController> logger,
        IClinicalPdfPreviewService pdfPreview,
        IClinicalArtifactService artifacts)
    {
        _encounterService = encounterService;
        _logger = logger;
        _pdfPreview = pdfPreview;
        _artifacts = artifacts;
    }

    [HttpGet("api/patient-encounters/{encounterUid:guid}/final-pdf")]
    public async Task<IActionResult> GetFinalPdf(Guid encounterUid, CancellationToken cancellationToken)
    {
        if (encounterUid == Guid.Empty) return BadRequest();
        var artifact = await _artifacts.OpenEncounterFinalPdfAsync(encounterUid, cancellationToken);
        return artifact is null
            ? NotFound(new { message = "The final PDF artifact was not found." })
            : File(artifact.Content, artifact.MimeType, artifact.FileName, enableRangeProcessing: true);
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
    [RequirePermission(PermissionKeys.EncountersEdit)]
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

    [HttpPost("api/patients/{patientUid:guid}/encounters")]
    [RequirePermission(PermissionKeys.EncountersEdit)]
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
        catch (UnauthorizedAccessException)
        {
            return Forbid();
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
    [RequirePermission(PermissionKeys.EncountersEdit)]
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
    [RequirePermission(PermissionKeys.EncountersEdit)]
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

    [HttpPost("api/patient-encounters/{encounterUid:guid}/pdf-preview")]
    [RequirePermission(PermissionKeys.EncountersEdit)]
    [Produces("application/pdf")]
    public async Task<IActionResult> PreviewPdf(Guid encounterUid, [FromBody] TemplatePreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (encounterUid == Guid.Empty) return BadRequest();
        try
        {
            var pdf = await _pdfPreview.PreviewEncounterAsync(encounterUid, request, cancellationToken);
            return pdf is null ? NotFound() : File(pdf, "application/pdf");
        }
        catch (MicroEMR.Application.Templates.Runtime.TemplateInstanceValidationException exception)
        {
            foreach (var error in exception.Errors) ModelState.AddModelError(error.Path, error.Message);
            return ValidationProblem(ModelState);
        }
        catch (InvalidOperationException exception) when (exception is not PdfRenderingException)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (PdfRenderingException exception)
        {
            _logger.LogError(exception, "PDF preview engine failed for encounter {EncounterUid}.", encounterUid);
            return Problem("PDF preview is temporarily unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPut("api/patients/{patientUid:guid}/encounters/{encounterUid:guid}/structured-data")]
    [RequirePermission(PermissionKeys.EncountersEdit)]
    public async Task<ActionResult<PatientEncounterDetailsResponse>> UpdateStructuredData(
        Guid patientUid, Guid encounterUid, [FromBody] UpdateEncounterStructuredDataRequest request,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || encounterUid == Guid.Empty) return BadRequest();
        try
        {
            var encounter = await _encounterService.UpdateStructuredDataAsync(
                patientUid, encounterUid, request, GetAuthenticatedUserId(), cancellationToken);
            return encounter is null ? NotFound() : Ok(encounter);
        }
        catch (MicroEMR.Application.Templates.Runtime.TemplateInstanceValidationException exception)
        {
            foreach (var error in exception.Errors) ModelState.AddModelError(error.Path, error.Message);
            return ValidationProblem(ModelState);
        }
        catch (EncounterNoteNotEditableException)
        {
            return Conflict(new { message = "The encounter changed or cannot be edited." });
        }
    }

    [HttpPost("api/patients/{patientUid:guid}/encounters/{encounterUid:guid}/sign")]
    [RequirePermission(PermissionKeys.EncountersSign)]
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
        catch (MicroEMR.Application.Templates.Runtime.TemplateInstanceValidationException exception)
        {
            foreach (var error in exception.Errors) ModelState.AddModelError(error.Path, error.Message);
            return ValidationProblem(ModelState);
        }
        catch (LinkedAppointmentCannotBeCompletedException)
        {
            return Conflict(new
            {
                message = "The linked appointment is no longer in a state that can be completed."
            });
        }
        catch (LinkedAppointmentNotFoundException exception)
        {
            _logger.LogError(
                exception,
                "Encounter {EncounterUid} references a missing appointment.",
                encounterUid);
            return Conflict(new
            {
                message = "The linked appointment could not be found."
            });
        }
    }

    private long GetAuthenticatedUserId() =>
        ClinicalUserActorContext.GetRequired(HttpContext);

    private string? GetAuthenticatedDisplayName()
    {
        return User.FindFirstValue("name")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.Identity?.Name;
    }
}
