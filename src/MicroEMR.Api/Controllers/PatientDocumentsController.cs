using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Services;

namespace MicroEMR.Api.Controllers;

[ApiController]
//[Authorize]
public sealed class PatientDocumentsController : ControllerBase
{
    private readonly IPatientDocumentService _documentService;
    private readonly ILogger<PatientDocumentsController> _logger;

    public PatientDocumentsController(
        IPatientDocumentService documentService,
        ILogger<PatientDocumentsController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    [HttpGet("api/patients/{patientUid:guid}/documents")]
    [ProducesResponseType<
        IReadOnlyList<PatientDocumentListItemResponse>>(
        StatusCodes.Status200OK)]
    public async Task<ActionResult<
        IReadOnlyList<PatientDocumentListItemResponse>>>
        GetPatientDocuments(
            Guid patientUid,
            CancellationToken cancellationToken)
    {
        var documents =
            await _documentService.GetByPatientUidAsync(
                patientUid,
                cancellationToken);

        return Ok(documents);
    }

    [HttpGet("api/patient-documents/{documentUid:guid}")]
    [ProducesResponseType<PatientDocumentDetailsResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientDocumentDetailsResponse>>
        GetDocument(
            Guid documentUid,
            CancellationToken cancellationToken)
    {
        var document =
            await _documentService.GetByUidAsync(
                documentUid,
                cancellationToken);

        if (document is null)
        {
            return NotFound(new
            {
                message = "The requested document was not found."
            });
        }

        return Ok(document);
    }

    [HttpPost("api/patients/{patientUid:guid}/documents")]
    [ProducesResponseType<PatientDocumentDetailsResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientDocumentDetailsResponse>>
        CreateDocument(
            Guid patientUid,
            [FromBody] CreatePatientDocumentRequest request,
            CancellationToken cancellationToken)
    {
        if (request.TemplateUid.HasValue)
        {
            var template =
                await _documentService.GetTemplateByUidAsync(
                    request.TemplateUid.Value,
                    cancellationToken);

            if (template is null || !template.IsActive)
            {
                ModelState.AddModelError(
                    nameof(request.TemplateUid),
                    "The selected document template is invalid or inactive.");

                return ValidationProblem(ModelState);
            }
        }

        var createdBy = GetAuthenticatedUserId();

        try
        {
            var document =
                await _documentService.CreateAsync(
                    patientUid,
                    request,
                    createdBy,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetDocument),
                new
                {
                    documentUid = document.DocumentUid
                },
                document);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to create document for patient {PatientUid}.",
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
                "Failed to create document for patient {PatientUid}.",
                patientUid);

            throw;
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
}
