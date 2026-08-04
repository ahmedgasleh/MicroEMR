using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.TenantUserAdministration;

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
}
