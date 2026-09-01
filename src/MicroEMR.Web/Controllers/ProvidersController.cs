using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Providers;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.Providers;

namespace MicroEMR.Web.Controllers;

[Authorize,RequireWebPermission(PermissionKeys.ProvidersView)]
public sealed class ProvidersController(IProviderAdministrationApiClient client,IWebPermissionService permissions,ILogger<ProvidersController> logger):Controller
{
 [HttpGet]public async Task<IActionResult>Index(string status="Active",CancellationToken token=default){if(status is not("Active" or "Inactive" or "All"))status="Active";return View(new ProviderIndexViewModel(status,await client.List(status,token),await permissions.HasAsync(PermissionKeys.ProvidersManage,token)));}
 [HttpGet,RequireWebPermission(PermissionKeys.ProvidersManage)]public IActionResult Add()=>View("Edit",new ProviderEditViewModel(null,new SaveProviderRequest()));
 [HttpGet,RequireWebPermission(PermissionKeys.ProvidersManage)]public async Task<IActionResult>Edit(Guid uid,CancellationToken token)=>await client.Get(uid,token)is{}x?View(new ProviderEditViewModel(uid,new(){FirstName=x.FirstName,LastName=x.LastName,DisplayName=x.DisplayName,ProviderType=x.ProviderType,BillingNumber=x.BillingNumber,Specialty=x.Specialty,RowVersion=x.RowVersion})):NotFound();
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ProvidersManage)]public async Task<IActionResult>Save(Guid? uid,SaveProviderRequest request,CancellationToken token){if(!ModelState.IsValid)return View("Edit",new ProviderEditViewModel(uid,request));try{if(uid.HasValue)await client.Update(uid.Value,request,token);else await client.Create(request,token);TempData["SuccessMessage"]="Provider saved.";return RedirectToAction(nameof(Index));}catch(HttpRequestException e){logger.LogWarning(e,"Provider save rejected.");ModelState.AddModelError("",e.Message);return View("Edit",new ProviderEditViewModel(uid,request));}}
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ProvidersManage)]public Task<IActionResult>Deactivate(Guid uid,string rowVersion,CancellationToken token)=>Status(uid,false,rowVersion,token);
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ProvidersManage)]public Task<IActionResult>Reactivate(Guid uid,string rowVersion,CancellationToken token)=>Status(uid,true,rowVersion,token);
 [HttpGet,RequireWebPermission(PermissionKeys.ProvidersManage)]public async Task<IActionResult>Link(Guid uid,CancellationToken token){var p=await client.Get(uid,token);return p is null?NotFound():View(new ProviderLinkViewModel(p,await client.Eligible(uid,token)));}
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ProvidersManage)]public async Task<IActionResult>Link(Guid uid,Guid applicationUserUid,string rowVersion,CancellationToken token){try{await client.Link(uid,new(applicationUserUid,rowVersion),false,token);TempData["SuccessMessage"]="Provider linked to user.";}catch(HttpRequestException e){TempData["ErrorMessage"]=e.Message;}return RedirectToAction(nameof(Index));}
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ProvidersManage)]public async Task<IActionResult>Unlink(Guid uid,Guid applicationUserUid,string rowVersion,CancellationToken token){try{await client.Link(uid,new(applicationUserUid,rowVersion),true,token);TempData["SuccessMessage"]="Provider user link removed.";}catch(HttpRequestException e){TempData["ErrorMessage"]=e.Message;}return RedirectToAction(nameof(Index));}
 async Task<IActionResult>Status(Guid uid,bool active,string version,CancellationToken token){try{await client.SetActive(uid,active,version,token);TempData["SuccessMessage"]=active?"Provider reactivated.":"Provider deactivated.";}catch(HttpRequestException e){TempData["ErrorMessage"]=e.Message;}return RedirectToAction(nameof(Index),new{status=active?"Inactive":"Active"});}
}
public sealed record ProviderIndexViewModel(string Status,IReadOnlyList<ProviderAdministrationItem> Providers,bool CanManage);
public sealed record ProviderEditViewModel(Guid? ProviderUid,SaveProviderRequest Provider);
public sealed record ProviderLinkViewModel(ProviderAdministrationItem Provider,IReadOnlyList<EligibleApplicationUser> Users);
