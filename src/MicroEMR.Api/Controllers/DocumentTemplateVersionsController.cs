using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientDocuments;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.Templates;
using MicroEMR.Application.Templates.Contracts;
using MicroEMR.Application.Templates.Services;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.ClinicalUsers;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/document-templates/{templateUid:guid}/versions")]
public sealed class DocumentTemplateVersionsController(
    IDocumentTemplateVersionService service,
    IPatientDocumentService documents,
    ITemplateAuthorizationService templateAuthorization,
    IAuthorizationService authorization,
    IAuthenticatedClinicalUserAccessor clinicalUsers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentTemplateVersionResponse>>> GetVersions(
        Guid templateUid,
        CancellationToken cancellationToken)
    {
        if (!await CanAccess(templateUid, false, cancellationToken)) return Forbid();
        return Ok(await service.GetVersionsAsync(templateUid, cancellationToken));
    }

    [HttpPost("draft")]
    public async Task<ActionResult<DocumentTemplateVersionResponse>> CreateDraft(
        Guid templateUid,
        CancellationToken cancellationToken)
    {
        if (!await CanAccess(templateUid, true, cancellationToken)) return Forbid();
        var version = await service.CreateDraftVersionAsync(templateUid, UserId(), cancellationToken);
        return version is null ? NotFound() : Ok(version);
    }

    [HttpPut("{templateVersionUid:guid}")]
    public async Task<ActionResult<DocumentTemplateVersionResponse>> UpdateDraft(
        Guid templateUid,
        Guid templateVersionUid,
        UpdateDocumentTemplateVersionRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccess(templateUid, true, cancellationToken)) return Forbid();
        return await Mutate(() => service.UpdateDraftVersionAsync(templateUid, templateVersionUid, request, UserId(), cancellationToken));
    }

    [HttpPost("{templateVersionUid:guid}/publish")]
    public async Task<ActionResult<DocumentTemplateVersionResponse>> Publish(
        Guid templateUid,
        Guid templateVersionUid,
        ChangeDocumentTemplateVersionStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccess(templateUid, true, cancellationToken)) return Forbid();
        return await Mutate(() => service.PublishVersionAsync(templateUid, templateVersionUid, request, UserId(), cancellationToken));
    }

    [HttpPost("{templateVersionUid:guid}/retire")]
    public async Task<ActionResult<DocumentTemplateVersionResponse>> Retire(
        Guid templateUid,
        Guid templateVersionUid,
        ChangeDocumentTemplateVersionStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccess(templateUid, true, cancellationToken)) return Forbid();
        return await Mutate(() => service.RetireVersionAsync(templateUid, templateVersionUid, request, UserId(), cancellationToken));
    }

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
        catch (TemplateDefinitionValidationException exception)
        {
            return new BadRequestObjectResult(new { message = exception.Message, errors = exception.Errors });
        }
    }

    private long UserId() => ClinicalUserActorContext.GetRequired(HttpContext);

    private async Task<bool> CanAccess(Guid templateUid,bool mutate,CancellationToken token)
    {
        var template=await documents.GetTemplateByUidAsync(templateUid,token);
        if(template is null)return false;
        var admin=(await authorization.AuthorizeAsync(User,null,TenantAuthorizationPolicies.ClinicAdministrator)).Succeeded;
        long userId;
        if(mutate) userId=UserId();
        else if(!ClinicalUserActorContext.TryGet(HttpContext,out userId))
        {
            try{userId=await clinicalUsers.GetRequiredUserIdAsync(token);}catch(ClinicalUserResolutionException){userId=0;}
        }
        var context=new TemplateAccessContext(userId,admin);
        return mutate?templateAuthorization.CanMutate(template,context):templateAuthorization.CanView(template,context);
    }
}
