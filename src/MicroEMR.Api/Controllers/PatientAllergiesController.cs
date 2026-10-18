using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientAllergies;
using MicroEMR.Application.PatientAllergies.Contracts;
using MicroEMR.Application.PatientAllergies.Services;
using MicroEMR.Application.ReadAudit;

namespace MicroEMR.Api.Controllers;

[ApiController,Authorize,RequirePermission(PermissionKeys.PatientsView)]
public sealed class PatientAllergiesController(IPatientAllergyService service,ILogger<PatientAllergiesController> logger,IPatientChartReadAuditService readAudit) : ControllerBase
{
    [HttpGet("api/patients/{patientUid:guid}/allergies")]
    public Task<IActionResult> GetPatientAllergies(Guid patientUid,CancellationToken token)=>Read(async()=>Ok((await Snapshot(patientUid,token)).Items));
    [HttpGet("api/patients/{patientUid:guid}/allergies/snapshot")]
    public Task<IActionResult> GetSnapshot(Guid patientUid,CancellationToken token)=>Read(async()=>Ok(await Snapshot(patientUid,token)));
    [HttpGet("api/patients/{patientUid:guid}/allergies/documentation-state")]
    public Task<IActionResult> GetDocumentationState(Guid patientUid,CancellationToken token)=>Read(async()=>Ok((await Snapshot(patientUid,token)).Documentation));
    private async Task<AllergySnapshot> Snapshot(Guid patient,CancellationToken token)
    {
        if(patient==Guid.Empty)throw new PatientAllergyNotFoundException();
        var data=await service.GetSnapshotAsync(patient,token);
        if(data.Items.Any(x=>x.PatientUid!=patient)||data.Documentation.NoKnownAllergies is {} assertion&&assertion.PatientUid!=patient)
            throw new UnauthorizedAccessException("Allergy patient context is unavailable.");
        await readAudit.RecordOpenedAsync(patient,HttpContext.TraceIdentifier,token);
        return data;
    }
    [HttpGet("api/patients/{patientUid:guid}/allergies/{allergyUid:guid}")]
    public Task<IActionResult> GetForPatient(Guid patientUid,Guid allergyUid,CancellationToken token)=>Read(async()=>
    {
        var row=await service.GetForPatientAsync(patientUid,allergyUid,token);
        if(row is null)return NotFound();
        if(row.PatientUid!=patientUid)throw new UnauthorizedAccessException();
        await readAudit.RecordOpenedAsync(patientUid,HttpContext.TraceIdentifier,token);return Ok(row);
    });
    // Legacy route now requires explicit patient context; never resolve a hidden form's patient from an unscoped UID.
    [HttpGet("api/patient-allergies/{allergyUid:guid}")]
    public Task<IActionResult> GetAllergy(Guid allergyUid,[FromQuery] Guid patientUid,CancellationToken token)
        =>patientUid==Guid.Empty?Task.FromResult<IActionResult>(BadRequest(new{message="Patient context is required."})):GetForPatient(patientUid,allergyUid,token);
    [HttpPost("api/patients/{patientUid:guid}/allergies"),RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> CreateAllergy(Guid patientUid,CreatePatientAllergyRequest request,CancellationToken token)
    {
        request.RequestCorrelationId=HttpContext.TraceIdentifier;
        return Write(async()=>{var row=await service.CreateAsync(patientUid,request,Actor(),null,token);return CreatedAtAction(nameof(GetForPatient),new{patientUid,allergyUid=row.AllergyUid},row);});
    }
    [HttpPut("api/patients/{patientUid:guid}/allergies/{allergyUid:guid}"),RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> UpdateAllergy(Guid patientUid,Guid allergyUid,UpdatePatientAllergyRequest request,CancellationToken token)
    {
        request.RequestCorrelationId=HttpContext.TraceIdentifier;
        return Write(async()=>await service.UpdateAsync(patientUid,allergyUid,request,Actor(),token) is {} row?Ok(row):NotFound());
    }
    [HttpPost("api/patients/{patientUid:guid}/allergies/{allergyUid:guid}/resolve"),RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> ResolveAllergy(Guid patientUid,Guid allergyUid,ResolvePatientAllergyRequest request,CancellationToken token)
    {
        request.RequestCorrelationId=HttpContext.TraceIdentifier;
        return Write(async()=>await service.ResolveAsync(patientUid,allergyUid,request,Actor(),token) is {} row?Ok(row):NotFound());
    }
    [HttpPost("api/patients/{patientUid:guid}/allergies/no-known-allergies"),RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> AssertNoKnownAllergies(Guid patientUid,CancellationToken token)
        =>Write(async()=>Ok(await service.AssertNoKnownAllergiesAsync(patientUid,Actor(),token,HttpContext.TraceIdentifier)));
    [HttpPost("api/patients/{patientUid:guid}/allergies/no-known-allergies/revoke"),RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> RevokeNoKnownAllergies(Guid patientUid,RevokeNoKnownAllergiesRequest request,CancellationToken token)
    {
        request.RequestCorrelationId=HttpContext.TraceIdentifier;
        return Write(async()=>await service.RevokeNoKnownAllergiesAsync(patientUid,request,Actor(),token) is {} row?Ok(row):NotFound());
    }
    private long Actor()=>ClinicalUserActorContext.GetRequired(HttpContext);
    private Task<IActionResult> Read(Func<Task<IActionResult>> action)=>Write(action);
    private async Task<IActionResult> Write(Func<Task<IActionResult>> action)
    {
        try{return await action();}
        catch(PatientAllergyValidationException e){return BadRequest(new{message=e.Message});}
        catch(PatientAllergyNotFoundException){return NotFound();}
        catch(PatientAllergyConcurrencyException e){return Conflict(new{message=e.Message});}
        catch(UnauthorizedAccessException){return Forbid();}
        catch(OperationCanceledException) when(HttpContext.RequestAborted.IsCancellationRequested){throw;}
        catch(Exception e){logger.LogError(e,"Allergy operation failed.");return StatusCode(503,new{message="Allergy information could not be processed. Reload and review before retrying."});}
    }
}
