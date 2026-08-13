using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.TenantUserAdministration;
using MicroEMR.Application.ClinicalUsers;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize(Policy = TenantAuthorizationPolicies.ClinicAdministrator)]
[Route("api/admin/users")]
public sealed class TenantUserAdministrationController(ITenantUserAdministrationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantUserAdministrationItem>>> Get(
        CancellationToken cancellationToken) =>
        Ok(await service.GetTenantUsersAsync(cancellationToken));

    [HttpGet("{authUserId}")]
    public async Task<ActionResult<TenantUserAdministrationItem>> GetUser(
        string authUserId, CancellationToken cancellationToken)
    {
        var user = await service.GetTenantUserAsync(authUserId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost("{authUserId}/membership/deactivate")]
    public Task<ActionResult<TenantUserAdministrationItem>> Deactivate(
        string authUserId, MembershipRowVersionRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(() => service.DeactivateMembershipAsync(authUserId, request.RowVersion, cancellationToken));

    [HttpPost("{authUserId}/membership/activate")]
    public Task<ActionResult<TenantUserAdministrationItem>> Activate(
        string authUserId, MembershipRowVersionRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(() => service.ActivateMembershipAsync(authUserId, request.RowVersion, cancellationToken));

    [HttpPut("{authUserId}/roles")]
    public Task<ActionResult<TenantUserAdministrationItem>> UpdateRoles(
        string authUserId, TenantRoleUpdateRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(() => service.UpdateTenantRolesAsync(authUserId, request.SelectedRoles ?? [], request.RowVersion, cancellationToken));

    [HttpPost("{authUserId}/clinical-user/provision")]
    public Task<ActionResult<TenantUserAdministrationItem>> ProvisionClinicalUser(
        string authUserId, CancellationToken cancellationToken) =>
        ChangeAsync(() => service.ProvisionClinicalUserAsync(authUserId, cancellationToken));

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
public sealed record TenantRoleUpdateRequest(IReadOnlyCollection<string>? SelectedRoles, string RowVersion);
