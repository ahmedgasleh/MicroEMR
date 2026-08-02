using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientDocuments;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Services;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/document-templates/{templateUid:guid}/versions")]
public sealed class DocumentTemplateVersionsController(
    IDocumentTemplateVersionService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentTemplateVersionResponse>>> GetVersions(
        Guid templateUid,
        CancellationToken cancellationToken) =>
        Ok(await service.GetVersionsAsync(templateUid, cancellationToken));

    [HttpPost("draft")]
    public async Task<ActionResult<DocumentTemplateVersionResponse>> CreateDraft(
        Guid templateUid,
        CancellationToken cancellationToken)
    {
        var version = await service.CreateDraftVersionAsync(templateUid, UserId(), cancellationToken);
        return version is null ? NotFound() : Ok(version);
    }

    [HttpPut("{templateVersionUid:guid}")]
    public async Task<ActionResult<DocumentTemplateVersionResponse>> UpdateDraft(
        Guid templateUid,
        Guid templateVersionUid,
        UpdateDocumentTemplateVersionRequest request,
        CancellationToken cancellationToken) =>
        await Mutate(() => service.UpdateDraftVersionAsync(
            templateUid, templateVersionUid, request, UserId(), cancellationToken));

    [HttpPost("{templateVersionUid:guid}/publish")]
    public async Task<ActionResult<DocumentTemplateVersionResponse>> Publish(
        Guid templateUid,
        Guid templateVersionUid,
        ChangeDocumentTemplateVersionStatusRequest request,
        CancellationToken cancellationToken) =>
        await Mutate(() => service.PublishVersionAsync(
            templateUid, templateVersionUid, request, UserId(), cancellationToken));

    [HttpPost("{templateVersionUid:guid}/retire")]
    public async Task<ActionResult<DocumentTemplateVersionResponse>> Retire(
        Guid templateUid,
        Guid templateVersionUid,
        ChangeDocumentTemplateVersionStatusRequest request,
        CancellationToken cancellationToken) =>
        await Mutate(() => service.RetireVersionAsync(
            templateUid, templateVersionUid, request, UserId(), cancellationToken));

    private static async Task<ActionResult<DocumentTemplateVersionResponse>> Mutate(
        Func<Task<DocumentTemplateVersionResponse?>> operation)
    {
        try
        {
            var version = await operation();
            return version is null ? new NotFoundResult() : new OkObjectResult(version);
        }
        catch (DocumentTemplateVersionConflictException exception)
        {
            return new ConflictObjectResult(new
            {
                message = exception.Message
            });
        }
    }

    private long UserId() => ClinicalUserActorContext.GetRequired(HttpContext);
}
