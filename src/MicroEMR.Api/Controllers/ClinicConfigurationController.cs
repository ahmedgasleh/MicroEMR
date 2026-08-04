using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.ClinicConfiguration;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize(Policy = TenantAuthorizationPolicies.ClinicAdministrator)]
[Route("api/clinic-configuration")]
public sealed class ClinicConfigurationController(
    IClinicConfigurationService service,
    ILogger<ClinicConfigurationController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ClinicConfigurationResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(cancellationToken));

    [HttpPut]
    public async Task<ActionResult<ClinicConfigurationResponse>> Save(
        [FromBody] SaveClinicConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.SaveAsync(request, cancellationToken));
        }
        catch (FormatException)
        {
            ModelState.AddModelError(nameof(request.RowVersion), "The row version is invalid.");
            return ValidationProblem(ModelState);
        }
        catch (ClinicConfigurationConcurrencyException)
        {
            return Conflict(new { message = "The clinic configuration changed. Reload it and try again." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save clinic configuration.");
            return Problem("Clinic configuration could not be saved.");
        }
    }
}
