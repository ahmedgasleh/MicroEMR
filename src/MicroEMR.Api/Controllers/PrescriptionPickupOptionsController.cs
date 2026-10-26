using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientPrescriptions;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, RequirePermission(PermissionKeys.PatientsView), RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PrescriptionPickupOptionsController(PrescriptionPickupPolicy policy) : ControllerBase
{
    [HttpGet("api/prescriptions/pickup-options")]
    public async Task<ActionResult<PickupTimeOptions>> Get(string? localTime, CancellationToken token)
    {
        try { return Ok(await policy.OptionsAsync(localTime, token)); }
        catch (PickupValidationException e) { return BadRequest(new { message = e.Message }); }
    }
}
