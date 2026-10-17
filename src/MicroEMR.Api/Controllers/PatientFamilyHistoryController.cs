using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientFamilyHistory;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, RequirePermission(PermissionKeys.PatientsView)]
[Route("api/patients/{patientUid:guid}/family-history")]
public sealed class PatientFamilyHistoryController(IPatientFamilyHistoryService service) : ControllerBase
{
    [HttpGet] public Task<IActionResult> List(Guid patientUid, string status = "Active", CancellationToken token = default) =>
        Run(async () => Ok(await service.ListAsync(patientUid, status, token)));
    [HttpGet("{familyHistoryUid:guid}")] public Task<IActionResult> Get(Guid patientUid, Guid familyHistoryUid, CancellationToken token) =>
        Run(async () => await service.GetAsync(patientUid, familyHistoryUid, token) is { } row ? Ok(row) : NotFound());
    [HttpPost, RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Create(Guid patientUid, CreatePatientFamilyHistoryRequest request, CancellationToken token) =>
        Run(async () => { var row = await service.CreateAsync(patientUid, request, Actor(), token); return CreatedAtAction(nameof(Get), new { patientUid, familyHistoryUid = row.FamilyHistoryUid }, row); });
    [HttpPut("{familyHistoryUid:guid}"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Update(Guid patientUid, Guid familyHistoryUid, UpdatePatientFamilyHistoryRequest request, CancellationToken token) =>
        Run(async () => await service.UpdateAsync(patientUid, familyHistoryUid, request, Actor(), token) is { } row ? Ok(row) : NotFound());
    [HttpPost("{familyHistoryUid:guid}/archive"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Archive(Guid patientUid, Guid familyHistoryUid, ArchivePatientFamilyHistoryRequest request, CancellationToken token) =>
        Run(async () => await service.ArchiveAsync(patientUid, familyHistoryUid, request.RowVersion, Actor(), token) is { } row ? Ok(row) : NotFound());
    private long Actor() => ClinicalUserActorContext.GetRequired(HttpContext);
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (PatientFamilyHistoryNotFoundException) { return NotFound(); }
        catch (PatientFamilyHistoryConcurrencyException) { return Conflict(new { message = "This family history entry changed. Reload before saving; your edits have been kept." }); }
        catch (PatientFamilyHistoryArchivedException) { return Conflict(new { message = "Archived family history cannot be changed." }); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
