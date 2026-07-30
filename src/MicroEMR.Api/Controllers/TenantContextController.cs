using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.Tenancy;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/context/tenant")]
public sealed class TenantContextController : ControllerBase
{
    private readonly ITenantContext _tenantContext;

    public TenantContextController(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        _tenantContext.TenantUid,
        _tenantContext.TenantKey,
        _tenantContext.DisplayName
    });
}
