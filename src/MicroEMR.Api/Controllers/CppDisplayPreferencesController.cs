using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientCpp;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/cpp/display-preferences")]
[RequirePermission(PermissionKeys.PatientsView)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CppDisplayPreferencesController(ICppDisplayPreferencesService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CppDisplayPreferencesResponse>> Get(CancellationToken cancellationToken)
    {
        try { return Ok(await service.GetAsync(cancellationToken)); }
        catch (ClinicalUserResolutionException) { return Forbid(); }
    }

    [HttpPut]
    public async Task<ActionResult<CppDisplayPreferencesResponse>> Save(
        SaveCppDisplayPreferencesRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await service.SaveAsync(request, cancellationToken)); }
        catch (ClinicalUserResolutionException) { return Forbid(); }
        catch (CppDisplayPreferencesConcurrencyException exception) { return Conflict(new { message = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
