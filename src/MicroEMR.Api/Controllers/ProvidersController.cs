using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Providers;

namespace MicroEMR.Api.Controllers;

[ApiController,Authorize,Route("api/providers"),RequirePermission(PermissionKeys.ProvidersView)]
public sealed class ProvidersController(IProviderAdministrationService service,ILogger<ProvidersController> logger):ControllerBase
{
    [HttpGet]public async Task<ActionResult<IReadOnlyList<ProviderAdministrationItem>>>List([FromQuery]string status="Active",CancellationToken token=default){try{return Ok(await service.ListAsync(status,token));}catch(ArgumentException e){return BadRequest(new{message=e.Message});}}
    [HttpGet("{providerUid:guid}")]public async Task<ActionResult<ProviderAdministrationItem>>Get(Guid providerUid,CancellationToken token)=>await service.GetAsync(providerUid,token)is{}x?Ok(x):NotFound();
    [HttpGet("eligible-users"),RequirePermission(PermissionKeys.ProvidersManage)]public async Task<ActionResult<IReadOnlyList<EligibleApplicationUser>>>EligibleUsers([FromQuery]Guid? providerUid,CancellationToken token)=>Ok(await service.EligibleUsersAsync(providerUid,token));
    [HttpPost,RequirePermission(PermissionKeys.ProvidersManage)]public Task<ActionResult<ProviderAdministrationItem>>Create(SaveProviderRequest request,CancellationToken token)=>Change(async()=>await service.CreateAsync(request,Actor(),token),true);
    [HttpPut("{providerUid:guid}"),RequirePermission(PermissionKeys.ProvidersManage)]public Task<ActionResult<ProviderAdministrationItem>>Update(Guid providerUid,SaveProviderRequest request,CancellationToken token)=>Change(()=>service.UpdateAsync(providerUid,request,Actor(),token));
    [HttpPost("{providerUid:guid}/deactivate"),RequirePermission(PermissionKeys.ProvidersManage)]public Task<ActionResult<ProviderAdministrationItem>>Deactivate(Guid providerUid,ProviderVersionRequest request,CancellationToken token)=>Change(()=>service.SetActiveAsync(providerUid,false,request.RowVersion,Actor(),token));
    [HttpPost("{providerUid:guid}/reactivate"),RequirePermission(PermissionKeys.ProvidersManage)]public Task<ActionResult<ProviderAdministrationItem>>Reactivate(Guid providerUid,ProviderVersionRequest request,CancellationToken token)=>Change(()=>service.SetActiveAsync(providerUid,true,request.RowVersion,Actor(),token));
    [HttpPost("{providerUid:guid}/link-user"),RequirePermission(PermissionKeys.ProvidersManage)]public Task<ActionResult<ProviderAdministrationItem>>Link(Guid providerUid,ProviderLinkRequest request,CancellationToken token)=>Change(()=>service.LinkAsync(providerUid,request,Actor(),token));
    [HttpDelete("{providerUid:guid}/link-user"),RequirePermission(PermissionKeys.ProvidersManage)]public Task<ActionResult<ProviderAdministrationItem>>Unlink(Guid providerUid,ProviderLinkRequest request,CancellationToken token)=>Change(()=>service.UnlinkAsync(providerUid,request,Actor(),token));
    private long Actor()=>ClinicalUserActorContext.GetRequired(HttpContext);
    private async Task<ActionResult<ProviderAdministrationItem>>Change(Func<Task<ProviderAdministrationItem?>> action,bool created=false){try{var x=await action();if(x is null)return NotFound();return created?CreatedAtAction(nameof(Get),new{providerUid=x.ProviderUid},x):Ok(x);}catch(ArgumentException e){return BadRequest(new{message=e.Message});}catch(ProviderConcurrencyException){return Conflict(new{message="Provider changed. Reload it and try again."});}catch(ProviderConflictException e){return Conflict(new{message=e.Message});}catch(Exception e){logger.LogError(e,"Provider administration operation failed.");return Problem("Provider administration operation could not be completed.");}}
}
