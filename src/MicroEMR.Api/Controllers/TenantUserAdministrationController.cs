using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.TenantUserAdministration;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/users")]
public sealed class TenantUserAdministrationController(ITenantUserAdministrationService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionKeys.UsersView)]
    public async Task<ActionResult<IReadOnlyList<TenantUserAdministrationItem>>> Get(
        CancellationToken cancellationToken) =>
        Ok(await service.GetTenantUsersAsync(cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionKeys.UsersManage)]
    public async Task<ActionResult<AddTenantUserResult>> AddUser(
        AddTenantUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.AddTenantUserAsync(request, cancellationToken);
            return result.ClinicalProvisioningFailed ? StatusCode(StatusCodes.Status207MultiStatus, result) : CreatedAtAction(
                nameof(GetUser), new { authUserId = result.User.AuthUserId }, result);
        }
        catch (TenantMembershipAlreadyExistsException ex) { return Conflict(new { message = ex.Message }); }
        catch (TenantRoleValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (TenantUserCreationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpGet("{authUserId}")]
    [RequirePermission(PermissionKeys.UsersView)]
    public async Task<ActionResult<TenantUserAdministrationItem>> GetUser(
        string authUserId, CancellationToken cancellationToken)
    {
        var user = await service.GetTenantUserAsync(authUserId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost("{authUserId}/membership/deactivate")]
    [RequirePermission(PermissionKeys.UsersManage)]
    public Task<ActionResult<TenantUserAdministrationItem>> Deactivate(
        string authUserId, MembershipRowVersionRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(() => service.DeactivateMembershipAsync(authUserId, request.RowVersion, cancellationToken));

    [HttpPost("{authUserId}/membership/activate")]
    [RequirePermission(PermissionKeys.UsersManage)]
    public Task<ActionResult<TenantUserAdministrationItem>> Activate(
        string authUserId, MembershipRowVersionRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(() => service.ActivateMembershipAsync(authUserId, request.RowVersion, cancellationToken));

    [HttpPut("{authUserId}/roles")]
    [RequirePermission(PermissionKeys.UsersManageAccess)]
    public Task<ActionResult<TenantUserAdministrationItem>> UpdateRoles(
        string authUserId, TenantRoleUpdateRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(() => service.UpdateTenantRolesAsync(authUserId, request.SelectedRoles ?? [], request.RowVersion, cancellationToken));

    [HttpPost("{authUserId}/clinical-user/provision")]
    [RequirePermission(PermissionKeys.UsersManage)]
    public Task<ActionResult<TenantUserAdministrationItem>> ProvisionClinicalUser(
        string authUserId, CancellationToken cancellationToken) =>
        ChangeAsync(() => service.ProvisionClinicalUserAsync(authUserId, cancellationToken));

    [HttpPost("{authUserId}/password/reset")]
    [RequirePermission(PermissionKeys.UsersManageAccess)]
    public async Task<IActionResult> ResetPassword(string authUserId,ResetTenantUserPasswordRequest request,CancellationToken cancellationToken)
    {try{await service.ResetPasswordAsync(authUserId,request.TemporaryPassword,cancellationToken);return NoContent();}catch(ArgumentException ex){return BadRequest(new{message=ex.Message});}catch(KeyNotFoundException){return NotFound();}catch(TenantMembershipNotFoundException){return NotFound();}}

    private async Task<ActionResult<TenantUserAdministrationItem>> ChangeAsync(
        Func<Task<TenantUserAdministrationItem>> action)
    {
        try { return Ok(await action()); }
        catch (FormatException) { return BadRequest(new { message = "The membership row version is invalid." }); }
        catch (TenantMembershipNotFoundException) { return NotFound(new { message = "The membership was not found in this clinic." }); }
        catch (TenantMembershipConcurrencyException) { return Conflict(new { message = "This membership was changed by another administrator." }); }
        catch (TenantMembershipSelfDeactivationException ex) { return Conflict(new { message = ex.Message }); }
        catch (TenantMembershipLastAdministratorException ex) { return Conflict(new { message = ex.Message }); }
        catch (TenantMembershipTransitionException ex) { return Conflict(new { message = ex.Message }); }
        catch (TenantRoleInactiveMembershipException ex) { return Conflict(new { message = ex.Message }); }
        catch (TenantRoleSelfLockoutException ex) { return Conflict(new { message = ex.Message }); }
        catch (TenantRoleValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (TenantClinicalProvisioningIdentityNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (TenantClinicalProvisioningNotEligibleException ex) { return Conflict(new { message = ex.Message }); }
        catch (ClinicalUserProvisioningConflictException ex) { return Conflict(new { message = ex.Message }); }
    }
}

public sealed record MembershipRowVersionRequest(string RowVersion);
public sealed record ResetTenantUserPasswordRequest(string TemporaryPassword);
public sealed record TenantRoleUpdateRequest(IReadOnlyCollection<string>? SelectedRoles, string RowVersion);
