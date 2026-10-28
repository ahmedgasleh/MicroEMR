using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientPrescriptions;

namespace MicroEMR.Api.Controllers;

[ApiController,Authorize,Route("api/dispensing-locations")]
public sealed class DispensingLocationsController(DispensingLocationDirectory directory):ControllerBase
{
    [HttpGet,RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
    public Task<IActionResult> Search(string? query,int page=1,int pageSize=20,CancellationToken token=default)=>Read(()=>directory.SearchAsync(query,false,page,pageSize,token));
    [HttpGet("maintenance"),RequirePermission(PermissionKeys.ClinicSettingsManage)]
    public Task<IActionResult> Maintenance(string? query,bool includeInactive=false,int page=1,int pageSize=20,CancellationToken token=default)=>Read(()=>directory.SearchAsync(query,includeInactive,page,pageSize,token));
    [HttpGet("{uid:guid}"),RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
    public Task<IActionResult> Get(Guid uid,CancellationToken token)=>Read(()=>directory.GetAsync(uid,token));
    [HttpGet("maintenance/{uid:guid}"),RequirePermission(PermissionKeys.ClinicSettingsManage)]
    public Task<IActionResult> MaintenanceGet(Guid uid,CancellationToken token)=>Read(()=>directory.GetAsync(uid,token));
    [HttpGet("resolve"),RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
    public Task<IActionResult> Resolve(string system,string identifier,CancellationToken token)=>Read(()=>directory.ResolveAsync(system,identifier,token));
    [HttpPost,RequirePermission(PermissionKeys.ClinicSettingsManage)]
    public Task<IActionResult> Create(SaveDispensingLocation request,CancellationToken token)=>Read(()=>directory.SaveAsync(null,"Create",request,ClinicalUserActorContext.GetRequired(HttpContext),token));
    [HttpPut("{uid:guid}"),RequirePermission(PermissionKeys.ClinicSettingsManage)]
    public Task<IActionResult> Edit(Guid uid,SaveDispensingLocation request,CancellationToken token)=>Read(()=>directory.SaveAsync(uid,"Edit",request,ClinicalUserActorContext.GetRequired(HttpContext),token));
    [HttpPost("{uid:guid}/deactivate"),RequirePermission(PermissionKeys.ClinicSettingsManage)]
    public Task<IActionResult> Deactivate(Guid uid,SaveDispensingLocation request,CancellationToken token)=>Read(()=>directory.SaveAsync(uid,"Deactivate",request,ClinicalUserActorContext.GetRequired(HttpContext),token));
    [HttpPost("{uid:guid}/reactivate"),RequirePermission(PermissionKeys.ClinicSettingsManage)]
    public Task<IActionResult> Reactivate(Guid uid,SaveDispensingLocation request,CancellationToken token)=>Read(()=>directory.SaveAsync(uid,"Reactivate",request,ClinicalUserActorContext.GetRequired(HttpContext),token));
    private async Task<IActionResult> Read<T>(Func<Task<T>> action)
    {
        try{var value=await action();return value is null?NotFound():Ok(value);}
        catch(ArgumentException e){return BadRequest(new{message=e.Message});}
        catch(DispensingLocationConflictException e){return Conflict(new{message=e.Message});}
    }
}
