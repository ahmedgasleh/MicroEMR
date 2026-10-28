using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.PatientPrescriptions;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class DispensingLocationsController(DispensingLocationApiClient client):Controller
{
    [HttpGet,RequireWebPermission(PermissionKeys.ClinicSettingsManage)]
    public async Task<IActionResult> Index(string? query,bool includeInactive=false,int page=1,CancellationToken token=default)=>View(new DispensingLocationIndex(await client.Search(query,true,includeInactive,page,token),query,includeInactive));
    [HttpGet,RequireWebPermission(PermissionKeys.ClinicSettingsManage)]
    public IActionResult Add()=>View("Edit",new DispensingLocationEdit(null,new()));
    [HttpGet,RequireWebPermission(PermissionKeys.ClinicSettingsManage)]
    public async Task<IActionResult> Edit(Guid uid,CancellationToken token){var x=await client.Get(uid,true,token);return x is null?NotFound():View(new DispensingLocationEdit(uid,new(){System=x.System,Identifier=x.Identifier,FacilityName=x.FacilityName,FacilityAddress=x.FacilityAddress,Phone=x.Phone,RowVersion=x.RowVersion}));}
    [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ClinicSettingsManage)]
    public Task<IActionResult> Add(SaveDispensingLocation request,CancellationToken token)=>Save(null,request,token);
    [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ClinicSettingsManage)]
    public Task<IActionResult> Edit(Guid uid,SaveDispensingLocation request,CancellationToken token)=>Save(uid,request,token);
    private async Task<IActionResult> Save(Guid? uid,SaveDispensingLocation request,CancellationToken token)
    {
        if(ModelState.IsValid)try{await client.Save(uid,uid.HasValue?"Edit":"Create",request,token);return RedirectToAction(nameof(Index));}catch(HttpRequestException e){ModelState.AddModelError("",e.Message);}
        return View("Edit",new DispensingLocationEdit(uid,request));
    }
    [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ClinicSettingsManage)]
    public Task<IActionResult> Deactivate(Guid uid,string rowVersion,CancellationToken token)=>Status(uid,rowVersion,"Deactivate",token);
    [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ClinicSettingsManage)]
    public Task<IActionResult> Reactivate(Guid uid,string rowVersion,CancellationToken token)=>Status(uid,rowVersion,"Reactivate",token);
    private async Task<IActionResult> Status(Guid uid,string version,string mode,CancellationToken token){try{await client.Save(uid,mode,new(){RowVersion=version},token);}catch(HttpRequestException e){TempData["ErrorMessage"]=e.Message;}return RedirectToAction(nameof(Index));}
    [HttpGet,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)]
    public async Task<IActionResult> Lookup(string? query,int page=1,CancellationToken token=default){try{return Json(await client.Search(query,false,false,page,token));}catch(HttpRequestException){return BadRequest(new{message="Directory search unavailable. Manual reference entry remains available."});}}
    [HttpGet,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)]
    public async Task<IActionResult> LookupDetail(Guid uid,CancellationToken token){try{var x=await client.Get(uid,false,token);return x is null?NotFound():Json(x);}catch(HttpRequestException){return BadRequest(new{message="Location unavailable. Search again."});}}
}
public sealed record DispensingLocationIndex(DispensingLocationPage Results,string? Query,bool IncludeInactive);
public sealed record DispensingLocationEdit(Guid? Uid,SaveDispensingLocation Request);
