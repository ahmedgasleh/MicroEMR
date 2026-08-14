using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Api.Controllers;

[ApiController,Authorize,RequirePermission(PermissionKeys.UsersManageAccess),Route("api/admin/access-profiles")]
public sealed class AccessProfilesController(IAccessProfileService service):ControllerBase
{
    [HttpGet("permissions")] public ActionResult<IReadOnlyList<BusinessPermission>> Permissions()=>Ok(PermissionCatalog.All);
    [HttpGet] public async Task<ActionResult<IReadOnlyList<AccessProfileSummary>>> List(CancellationToken token)=>Ok(await service.ListAsync(token));
    [HttpGet("{uid:guid}")] public async Task<ActionResult<AccessProfileDetails>> Get(Guid uid,CancellationToken token){var x=await service.GetAsync(uid,token);return x is null?NotFound():Ok(x);}
    [HttpPut("{uid:guid}/permissions")] public async Task<IActionResult> Update(Guid uid,UpdateAccessProfilePermissionsRequest request,CancellationToken token)
    {try{await service.UpdatePermissionsAsync(uid,request.PermissionKeys??[],request.RowVersion,token);return NoContent();}catch(ArgumentException ex){return BadRequest(new{message=ex.Message});}catch(FormatException){return BadRequest(new{message="Invalid row version."});}catch(KeyNotFoundException){return NotFound();}catch(InvalidOperationException ex){return Conflict(new{message=ex.Message});}}
    [HttpPut("users/{authUserId}")] public async Task<IActionResult> Assign(string authUserId,AssignUserAccessProfileRequest request,CancellationToken token)
    {try{await service.AssignAsync(authUserId,request.AccessProfileUid,request.RowVersion,token);return NoContent();}catch(FormatException){return BadRequest(new{message="Invalid row version."});}catch(KeyNotFoundException){return NotFound();}catch(InvalidOperationException ex){return Conflict(new{message=ex.Message});}}
    [HttpGet("users/{authUserId}/effective")] public async Task<ActionResult<IReadOnlyCollection<string>>> Effective(string authUserId,CancellationToken token)=>Ok(await service.GetEffectivePermissionsAsync(authUserId,token));
    [HttpGet("users/{authUserId}/access")] public async Task<ActionResult<UserEffectiveAccess>> Access(string authUserId,CancellationToken token)
    {var result=await service.GetUserAccessAsync(authUserId,token);return result is null?NotFound():Ok(result);}
    [HttpPut("users/{authUserId}/overrides/{permissionKey}")] public async Task<IActionResult> SetOverride(string authUserId,string permissionKey,UpdateUserPermissionOverrideRequest request,CancellationToken token)
    {try{if(!Enum.TryParse<PermissionOverrideState>(request.OverrideState,true,out var state))return BadRequest(new{message="Override must be Inherit, Allow, or Deny."});await service.SetUserOverrideAsync(authUserId,permissionKey,state,request.RowVersion,token);return NoContent();}catch(ArgumentException ex){return BadRequest(new{message=ex.Message});}catch(FormatException){return BadRequest(new{message="Invalid row version."});}catch(KeyNotFoundException){return NotFound();}catch(InvalidOperationException ex){return Conflict(new{message=ex.Message});}}
}

[ApiController, Authorize, Route("api/permissions/me")]
public sealed class EffectivePermissionsController(ICurrentUserPermissionService permissions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlySet<string>>> Get(CancellationToken token) =>
        Ok(await permissions.GetEffectivePermissionsAsync(token));
}
public sealed record UpdateAccessProfilePermissionsRequest(IReadOnlyCollection<string>? PermissionKeys,string RowVersion);
public sealed record AssignUserAccessProfileRequest(Guid AccessProfileUid,string RowVersion);
public sealed record UpdateUserPermissionOverrideRequest(string OverrideState,string RowVersion);
