using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using Microsoft.AspNetCore.Mvc;

using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.PatientDocuments;
using MicroEMR.Application.Templates.Contracts;
using MicroEMR.Application.Templates.Runtime;
using MicroEMR.Application.ClinicalOutput;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission(PermissionKeys.DocumentsView)]
public sealed class PatientDocumentsController : ControllerBase
{
    private readonly IPatientDocumentService _documentService;
    private readonly ILogger<PatientDocumentsController> _logger;
    private readonly IAuthorizationService _authorization;
    private readonly IClinicalPdfPreviewService _pdfPreview;

    public PatientDocumentsController(
        IPatientDocumentService documentService,
        ILogger<PatientDocumentsController> logger,
        IAuthorizationService authorization,
        IClinicalPdfPreviewService pdfPreview)
    {
        _documentService = documentService;
        _logger = logger;
        _authorization = authorization;
        _pdfPreview = pdfPreview;
    }

    [HttpPost("api/patient-documents/{documentUid:guid}/pdf-preview")]
    [RequirePermission(PermissionKeys.DocumentsManage)]
    [Produces("application/pdf")]
    public async Task<IActionResult> PreviewPdf(Guid documentUid, [FromBody] TemplatePreviewRequest request,
        CancellationToken cancellationToken)
    {
        if (documentUid == Guid.Empty) return BadRequest();
        try
        {
            var pdf = await _pdfPreview.PreviewPatientDocumentAsync(documentUid, request, cancellationToken);
            return pdf is null ? NotFound() : File(pdf, "application/pdf");
        }
        catch (TemplateInstanceValidationException exception)
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
            _logger.LogError(exception, "PDF preview engine failed for document {DocumentUid}.", documentUid);
            return Problem("PDF preview is temporarily unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
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

    [HttpPut("api/patient-documents/{documentUid:guid}/draft")]
    [RequirePermission(PermissionKeys.DocumentsManage)]
    [ProducesResponseType<PatientDocumentDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PatientDocumentDetailsResponse>> UpdateDraft(
        Guid documentUid,
        [FromBody] UpdatePatientDocumentDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (documentUid == Guid.Empty)
            return BadRequest();

        request.Title = request.Title?.Trim() ?? string.Empty;
        request.DocumentType = request.DocumentType?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(request.Title))
            ModelState.AddModelError(nameof(request.Title), "Document title is required.");
        if (string.IsNullOrWhiteSpace(request.DocumentType))
            ModelState.AddModelError(nameof(request.DocumentType), "Document type is required.");
        if (!IsValidRowVersion(request.RowVersion))
            ModelState.AddModelError(nameof(request.RowVersion), "A valid document row version is required.");
        if (!IsValidRowVersion(request.ContentRowVersion))
            ModelState.AddModelError(nameof(request.ContentRowVersion), "A valid content row version is required.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var document = await _documentService.UpdateDraftAsync(
                documentUid,
                request,
                GetAuthenticatedUserId(),
                cancellationToken);

            return document is null ? NotFound() : Ok(document);
        }
        catch (PatientDocumentNotDraftException)
        {
            return Conflict(new { message = "Only draft patient documents can be edited." });
        }
        catch (PatientDocumentConcurrencyException)
        {
            return Conflict(new
            {
                message = "This document was changed by another user. Reload it before saving again."
            });
        }
        catch (TemplateInstanceValidationException exception)
        {
            foreach (var error in exception.Errors) ModelState.AddModelError(error.Path, error.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpPost("api/patients/{patientUid:guid}/documents")]
    [RequirePermission(PermissionKeys.DocumentsManage)]
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
        if (!request.TemplateUid.HasValue)
        {
            if (string.IsNullOrWhiteSpace(request.Title)) ModelState.AddModelError(nameof(request.Title), "Document title is required.");
            if (string.IsNullOrWhiteSpace(request.DocumentType)) ModelState.AddModelError(nameof(request.DocumentType), "Document type is required.");
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
        }
        var createdBy = GetAuthenticatedUserId();

        try
        {
            var document =
                await _documentService.CreateAsync(
                    patientUid,
                    request,
                    createdBy,
                    await AccessContext(),
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
        catch (TemplateInstanceValidationException exception)
        {
            foreach (var error in exception.Errors) ModelState.AddModelError(error.Path, error.Message);
            return ValidationProblem(ModelState);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
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

    private long GetAuthenticatedUserId() =>
        ClinicalUserActorContext.GetRequired(HttpContext);

    private async Task<TemplateAccessContext> AccessContext() => new(GetAuthenticatedUserId(),
        (await _authorization.AuthorizeAsync(User, null,
            MicroEMR.Api.Authorization.TenantAuthorizationPolicies.ClinicAdministrator)).Succeeded);

    private static bool IsValidRowVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            return Convert.FromBase64String(value).Length == 8;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
