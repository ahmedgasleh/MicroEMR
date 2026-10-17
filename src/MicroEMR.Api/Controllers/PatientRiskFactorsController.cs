using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientRiskFactors;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, RequirePermission(PermissionKeys.PatientsView)]
[Route("api/patients/{patientUid:guid}/risk-factors")]
public sealed class PatientRiskFactorsController(IPatientRiskFactorService service) : ControllerBase
{
    [HttpGet] public Task<IActionResult> List(Guid patientUid, string status = "Active", CancellationToken token = default) =>
        Run(async () => Ok(await service.ListAsync(patientUid, status, token)));
    [HttpGet("{riskFactorUid:guid}")] public Task<IActionResult> Get(Guid patientUid, Guid riskFactorUid, CancellationToken token) =>
        Run(async () => await service.GetAsync(patientUid, riskFactorUid, token) is { } row ? Ok(row) : NotFound());
    [HttpPost, RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Create(Guid patientUid, CreatePatientRiskFactorRequest request, CancellationToken token) =>
        Run(async () => { var row = await service.CreateAsync(patientUid, request, Actor(), token); return CreatedAtAction(nameof(Get), new { patientUid, riskFactorUid = row.RiskFactorUid }, row); });
    [HttpPut("{riskFactorUid:guid}"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Update(Guid patientUid, Guid riskFactorUid, UpdatePatientRiskFactorRequest request, CancellationToken token) =>
        Run(async () => await service.UpdateAsync(patientUid, riskFactorUid, request, Actor(), token) is { } row ? Ok(row) : NotFound());
    [HttpPost("{riskFactorUid:guid}/archive"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Archive(Guid patientUid, Guid riskFactorUid, ArchivePatientRiskFactorRequest request, CancellationToken token) =>
        Run(async () => await service.ArchiveAsync(patientUid, riskFactorUid, request.RowVersion, Actor(), token) is { } row ? Ok(row) : NotFound());
    private long Actor() => ClinicalUserActorContext.GetRequired(HttpContext);
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (PatientRiskFactorNotFoundException) { return NotFound(); }
        catch (PatientRiskFactorConcurrencyException) { return Conflict(new { message = "This risk factor entry changed. Reload before saving; your edits have been kept." }); }
        catch (PatientRiskFactorArchivedException) { return Conflict(new { message = "Archived risk factor cannot be changed." }); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
