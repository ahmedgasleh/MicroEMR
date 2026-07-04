using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Services;

namespace MicroEMR.Api.Controllers;

[ApiController]
[AllowAnonymous] // For development only. Remove this attribute in production.
[Route("api/document-templates")]
public sealed class DocumentTemplatesController : ControllerBase
{
    private readonly IPatientDocumentService _documentService;

    public DocumentTemplatesController(
        IPatientDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet]
    [ProducesResponseType<
        IReadOnlyList<DocumentTemplateListItemResponse>>(
        StatusCodes.Status200OK)]
    public async Task<ActionResult<
        IReadOnlyList<DocumentTemplateListItemResponse>>> GetTemplates(
        CancellationToken cancellationToken)
    {
        var templates =
            await _documentService.GetActiveTemplatesAsync(
                cancellationToken);

        return Ok(templates);
    }

    [HttpGet("{templateUid:guid}")]
    [ProducesResponseType<DocumentTemplateDetailsResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentTemplateDetailsResponse>>
        GetTemplate(
            Guid templateUid,
            CancellationToken cancellationToken)
    {
        var template =
            await _documentService.GetTemplateByUidAsync(
                templateUid,
                cancellationToken);

        if (template is null || !template.IsActive)
        {
            return NotFound(new
            {
                message = "The requested template was not found."
            });
        }

        return Ok(template);
    }
}
