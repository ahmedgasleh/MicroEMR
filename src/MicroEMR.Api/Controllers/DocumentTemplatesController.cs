using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MicroEMR.Api.Models.PatientDocuments;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/document-templates")]
public sealed class DocumentTemplatesController : ControllerBase
{
    private readonly IPatientDocumentRepository _repository;

    public DocumentTemplatesController(
        IPatientDocumentRepository repository)
    {
        _repository = repository;
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
            await _repository.GetActiveTemplatesAsync(
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
            await _repository.GetTemplateByUidAsync(
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