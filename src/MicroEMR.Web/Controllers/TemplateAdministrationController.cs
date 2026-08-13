using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.Security;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models.TemplateAdministration;
using MicroEMR.Web.Services.TemplateAdministration;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Web.Controllers;

[Authorize]
[RequireWebPermission(PermissionKeys.TemplatesManage)]
public sealed class TemplateAdministrationController(ITemplateAdministrationApiClient client,ILogger<TemplateAdministrationController> logger):Controller
{
    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<IActionResult> Index(string status="All",string kind="All",string scope="All",CancellationToken token=default)
    {
        var apiStatus=status is "Inactive"?"Inactive":"All";var templates=await client.ListAsync(apiStatus,token);
        var versions=await Task.WhenAll(templates.Select(x=>client.GetVersionsAsync(x.TemplateUid,token)));
        var canManageClinic=User.HasClaim(MicroEmrClaimTypes.TenantRole,ClinicConfigurationAuthorization.Role);
        var items=templates.Select((template,index)=>
        {
            var draft=versions[index].OrderByDescending(x=>x.VersionNumber).FirstOrDefault(x=>x.Status=="Draft");
            var published=versions[index].FirstOrDefault(x=>x.IsCurrent&&x.Status=="Published");
            var versionStatus=!template.IsActive?"Inactive":draft is not null?"Draft":published is not null?"Published":"No version";
            return new TemplateAdministrationListItemViewModel{TemplateUid=template.TemplateUid,Name=template.TemplateName,TemplateKind=template.TemplateKind,Category=template.Category??template.TemplateKind,TemplateScope=template.TemplateScope,OwnerUserId=template.OwnerUserId,IsActive=template.IsActive,CurrentVersion=draft?.VersionNumber??published?.VersionNumber,VersionStatus=versionStatus,LastUpdated=template.UpdatedAt??template.CreatedAt,RowVersion=template.RowVersion??string.Empty,CanEdit=template.TemplateScope!="System"&&(template.TemplateScope=="Personal"||canManageClinic)};
        }).Where(x=>(kind=="All"||x.TemplateKind==kind)&&(scope=="All"||x.TemplateScope==scope)&&(status=="All"||x.VersionStatus==status)).ToArray();
        return View(new TemplateAdministrationIndexViewModel{Templates=items,Status=status,Kind=kind,Scope=scope,CanManageClinic=canManageClinic});
    }

    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTemplateViewModel model,CancellationToken token)
    {
        if(!ModelState.IsValid)return BadRequest(new{success=false,message="Please correct the template details."});
        try{var result=await client.CreateAsync(model,token);if(result?.DraftVersion is null)return BadRequest(new{success=false,message="The template draft was not created."});return Json(new{success=true,redirectUrl=Url.Action(nameof(Builder),new{templateUid=result.Template.TemplateUid,versionUid=result.DraftVersion.TemplateVersionUid})});}catch(TemplateAdministrationApiException ex){return ApiFailure(ex);}
    }

    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(Guid templateUid,CancellationToken token)
    {
        try{var version=await client.OpenDraftAsync(templateUid,token);return version is null?NotFound():RedirectToAction(nameof(Builder),new{templateUid,versionUid=version.TemplateVersionUid});}catch(TemplateAdministrationApiException ex){return ApiFailure(ex);}
    }

    [HttpGet]
    public async Task<IActionResult> Builder(Guid templateUid,Guid? versionUid,CancellationToken token)
    {
        var template=await client.GetAsync(templateUid,token);if(template is null)return NotFound();
        var versions=await client.GetVersionsAsync(templateUid,token);var version=versionUid.HasValue?versions.FirstOrDefault(x=>x.TemplateVersionUid==versionUid):versions.OrderByDescending(x=>x.VersionNumber).FirstOrDefault(x=>x.Status=="Draft")??versions.FirstOrDefault(x=>x.IsCurrent);
        if(version is null)return NotFound();
        TemplateDefinition definition;try{definition=JsonSerializer.Deserialize<TemplateDefinition>(version.DefinitionJson,JsonOptions)??new(){SchemaVersion=1,Sections=[]};}catch(JsonException){definition=new(){SchemaVersion=1,Sections=[]};}
        var canManageClinic=User.HasClaim(MicroEmrClaimTypes.TenantRole,ClinicConfigurationAuthorization.Role);
        var canEdit=template.TemplateScope!="System"&&(template.TemplateScope=="Personal"||canManageClinic);
        return View(new TemplateBuilderViewModel{Template=template,Version=version,Definition=definition,CanEdit=canEdit,IsReadOnly=!canEdit||version.Status!="Draft"});
    }

    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Validate([FromBody]SaveTemplateDefinitionViewModel model,CancellationToken token)
    {try{var result=await client.ValidateAsync(model.Definition,token);return StatusCode(result.IsValid?200:400,result);}catch(TemplateAdministrationApiException ex){return ApiFailure(ex);}}

    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody]SaveTemplateDefinitionViewModel model,CancellationToken token)
    {try{var result=await client.SaveDraftAsync(model,token);return result is null?NotFound():Json(new{success=true,version=result});}catch(TemplateAdministrationApiException ex){return ApiFailure(ex);}}

    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish([FromBody]PublishTemplateViewModel model,CancellationToken token)
    {try{var result=await client.PublishAsync(model,token);return result is null?NotFound():Json(new{success=true,version=result});}catch(TemplateAdministrationApiException ex){return ApiFailure(ex);}}

    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Clone(CloneTemplateViewModel model,CancellationToken token)
    {try{var result=await client.CloneAsync(model,token);if(result?.DraftVersion is null)return BadRequest(new{success=false,message="Clone draft was not created."});return Json(new{success=true,redirectUrl=Url.Action(nameof(Builder),new{templateUid=result.Template.TemplateUid,versionUid=result.DraftVersion.TemplateVersionUid})});}catch(TemplateAdministrationApiException ex){return ApiFailure(ex);}}

    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(SetTemplateActiveViewModel model,CancellationToken token)
    {try{var result=await client.SetActiveAsync(model,token);return result is null?NotFound():Json(new{success=true});}catch(TemplateAdministrationApiException ex){return ApiFailure(ex);}}

    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMetadata(UpdateTemplateMetadataViewModel model,CancellationToken token)
    {try{var result=await client.UpdateMetadataAsync(model,token);return result is null?NotFound():Json(new{success=true,template=result});}catch(TemplateAdministrationApiException ex){return ApiFailure(ex);}}

    private IActionResult ApiFailure(TemplateAdministrationApiException exception)
    {logger.LogWarning("Template administration request failed with status {StatusCode}.",exception.StatusCode);return new ContentResult{StatusCode=exception.StatusCode,ContentType="application/json",Content=exception.ResponseBody};}
}
