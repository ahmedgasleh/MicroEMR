using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using MicroEMR.Web.Authorization;using MicroEMR.Web.Services.TenantUserAdministration;
namespace MicroEMR.Web.Controllers;
[Authorize, RequireWebPermission(MicroEMR.Application.AccessProfiles.PermissionKeys.UsersManageAccess)] public sealed class AccessProfilesController(IAccessProfileApiClient client):Controller
{
 [HttpGet]public async Task<IActionResult> Index(CancellationToken t)=>View(await client.ListAsync(t));
 [HttpGet]public async Task<IActionResult> Details(Guid uid,CancellationToken t){var p=await client.GetAsync(uid,t);return p is null?NotFound():View(new AccessProfileDetailsViewModel(p,await client.PermissionsAsync(t)));}
 [HttpPost,ValidateAntiForgeryToken]public async Task<IActionResult> UpdatePermissions(Guid uid,string rowVersion,string[] permissionKeys,CancellationToken t){try{await client.UpdateAsync(uid,permissionKeys,rowVersion,t);TempData["SuccessMessage"]="Access profile permissions updated.";return RedirectToAction(nameof(Details),new{uid});}catch(HttpRequestException){TempData["ErrorMessage"]="The profile changed or could not be updated.";return RedirectToAction(nameof(Details),new{uid});}}
}
public sealed record AccessProfileDetailsViewModel(MicroEMR.Application.AccessProfiles.AccessProfileDetails Profile,IReadOnlyList<MicroEMR.Application.AccessProfiles.BusinessPermission> Catalog);
