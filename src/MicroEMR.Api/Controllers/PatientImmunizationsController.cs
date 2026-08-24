using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientImmunizations;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/patients/{patientUid:guid}/immunizations")]
[RequirePermission(PermissionKeys.PatientsView)]
public sealed class PatientImmunizationsController(IPatientImmunizationService service, ILogger<PatientImmunizationsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PatientImmunizationResponse>>> List(Guid patientUid, string status = "All", CancellationToken token = default) =>
        Ok(await service.ListAsync(patientUid, status, token));

    [HttpGet("{immunizationUid:guid}")]
    public async Task<ActionResult<PatientImmunizationResponse>> Get(Guid patientUid, Guid immunizationUid, CancellationToken token) =>
        await service.GetAsync(patientUid, immunizationUid, token) is { } item ? Ok(item) : NotFound();

    [HttpPost, RequirePermission(PermissionKeys.ClinicalDataManage)]
    public async Task<ActionResult<PatientImmunizationResponse>> Create(Guid patientUid, CreatePatientImmunizationRequest request, CancellationToken token)
    {
        var item = await service.CreateAsync(patientUid, request, Actor(), token);
        return CreatedAtAction(nameof(Get), new { patientUid, immunizationUid = item.ImmunizationUid }, item);
    }

    [HttpPut("{immunizationUid:guid}"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public async Task<ActionResult<PatientImmunizationResponse>> Update(Guid patientUid, Guid immunizationUid, UpdatePatientImmunizationRequest request, CancellationToken token)
    {
        try { return await service.UpdateAsync(patientUid, immunizationUid, request, Actor(), token) is { } item ? Ok(item) : NotFound(); }
        catch (PatientImmunizationConcurrencyException) { return Conflict(new { message = "This immunization was changed by another user." }); }
        catch (PatientImmunizationTerminalException) { return Conflict(new { message = "Entered-in-error immunizations cannot be edited." }); }
    }

    [HttpPost("{immunizationUid:guid}/entered-in-error"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public async Task<ActionResult<PatientImmunizationResponse>> MarkEnteredInError(Guid patientUid, Guid immunizationUid, MarkImmunizationEnteredInErrorRequest request, CancellationToken token)
    {
        try { return await service.MarkEnteredInErrorAsync(patientUid, immunizationUid, request, Actor(), token) is { } item ? Ok(item) : NotFound(); }
        catch (PatientImmunizationConcurrencyException) { return Conflict(new { message = "This immunization was changed by another user." }); }
        catch (PatientImmunizationTerminalException) { return Conflict(new { message = "This immunization is already entered in error." }); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unable to mark patient immunization entered in error.");
            throw;
        }
    }

    private long Actor() => ClinicalUserActorContext.GetRequired(HttpContext);
}
