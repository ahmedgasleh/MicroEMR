using Microsoft.AspNetCore.Authorization;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientDocuments;
using MicroEMR.Application.Templates;
using MicroEMR.Application.Templates.Contracts;
using MicroEMR.Application.Templates.Services;
using MicroEMR.Application.ClinicalUsers;

namespace MicroEMR.Api.Controllers;

[ApiController,Authorize,Route("api/document-templates/administration")]
[RequirePermission(PermissionKeys.TemplatesManage)]
public sealed class TemplateAdministrationController(
    ITemplateAdministrationService service, IAuthorizationService authorization,
    IAuthenticatedClinicalUserAccessor clinicalUsers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery]string status="Active",CancellationToken token=default) => Ok(await service.ListAsync(status,await Context(false,token),token));

    [HttpGet("{templateUid:guid}")]
    public async Task<IActionResult> Get(Guid templateUid,CancellationToken token)
    { var result=await service.GetAsync(templateUid,await Context(false,token),token);return result is null?NotFound():Ok(result); }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAdministrativeTemplateRequest request,CancellationToken token) =>
        await Execute(async()=>{var result=await service.CreateAsync(request,await Context(true,token),token);return result is null?BadRequest():CreatedAtAction(nameof(Get),new{templateUid=result.Template.TemplateUid},result);});

    [HttpPut("{templateUid:guid}/metadata")]
    public async Task<IActionResult> UpdateMetadata(Guid templateUid,UpdateAdministrativeTemplateMetadataRequest request,CancellationToken token) =>
        await Execute(async()=>{var result=await service.UpdateMetadataAsync(templateUid,request,await Context(true,token),token);return result is null?NotFound():Ok(result);});

    [HttpPost("{templateUid:guid}/clone")]
    public async Task<IActionResult> Clone(Guid templateUid,CloneDocumentTemplateRequest request,CancellationToken token) =>
        await Execute(async()=>{var result=await service.CloneAsync(templateUid,request,await Context(true,token),token);return result is null?NotFound():CreatedAtAction(nameof(Get),new{templateUid=result.Template.TemplateUid},result);});

    [HttpPost("{templateUid:guid}/set-active")]
    public async Task<IActionResult> SetActive(Guid templateUid,SetAdministrativeTemplateActiveRequest request,CancellationToken token)=>
        await Execute(async()=>{var result=await service.SetActiveAsync(templateUid,request,await Context(true,token),token);return result is null?NotFound():Ok(result);});

    private async Task<TemplateAccessContext> Context(bool mutation,CancellationToken token)
    {
        long userId;
        if(mutation)userId=ClinicalUserActorContext.GetRequired(HttpContext);
        else if(!ClinicalUserActorContext.TryGet(HttpContext,out userId))
        {try{userId=await clinicalUsers.GetRequiredUserIdAsync(token);}catch(ClinicalUserResolutionException){userId=0;}}
        return new(userId,(await authorization.AuthorizeAsync(User,null,TenantAuthorizationPolicies.ClinicAdministrator)).Succeeded);
    }

    private static async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    { try{return await action();}catch(TemplateDefinitionValidationException ex){return new BadRequestObjectResult(new{message=ex.Message,errors=ex.Errors});}catch(DocumentTemplateVersionConflictException ex){return new ConflictObjectResult(new{message=ex.Message});}catch(UnauthorizedAccessException){return new ForbidResult();}catch(ArgumentException ex){return new BadRequestObjectResult(new{message=ex.Message});} }
}
