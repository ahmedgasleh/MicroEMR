using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.PatientDocuments;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
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
    public async Task<IActionResult> GetTemplates(
        [FromQuery(Name = "status")] string status = "Active",
        CancellationToken cancellationToken = default)
    {
        var templates = await _documentService.GetTemplatesAsync(status, cancellationToken);

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

        if (template is null)
        {
            return NotFound(new
            {
                message = "The requested template was not found."
            });
        }

        return Ok(template);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<DocumentTemplateDetailsResponse>> CreateTemplate(
        [FromBody] CreateDocumentTemplateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var template = await _documentService.CreateTemplateAsync(request, GetAuthenticatedUserId(), cancellationToken);
        return template is null ? BadRequest() : CreatedAtAction(nameof(GetTemplate), new { templateUid = template.TemplateUid }, template);
    }

    [Authorize]
    [HttpPut("{templateUid:guid}")]
    public async Task<ActionResult<DocumentTemplateDetailsResponse>> UpdateTemplate(
        Guid templateUid, [FromBody] UpdateDocumentTemplateRequest request, CancellationToken cancellationToken)
    {
        if (templateUid == Guid.Empty || !ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var template = await _documentService.UpdateTemplateAsync(templateUid, request, GetAuthenticatedUserId(), cancellationToken);
            return template is null ? NotFound() : Ok(template);
        }
        catch (DocumentTemplateVersionConflictException)
        {
            return Conflict(new { message = "Published template content cannot be edited in place. Create a new draft version instead." });
        }
    }

    [Authorize]
    [HttpPost("{templateUid:guid}/set-active")]
    public async Task<ActionResult<DocumentTemplateDetailsResponse>> SetActive(
        Guid templateUid, [FromBody] SetDocumentTemplateActiveRequest request, CancellationToken cancellationToken)
    {
        if (templateUid == Guid.Empty) return BadRequest();
        var template = await _documentService.SetTemplateActiveAsync(templateUid, request.IsActive, GetAuthenticatedUserId(), cancellationToken);
        return template is null ? NotFound() : Ok(template);
    }

    private long? GetAuthenticatedUserId()
    {
        var value = User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var userId) ? userId : null;
    }
}
