using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientVitals.Contracts;
using MicroEMR.Application.PatientVitals.Services;
using MicroEMR.Application.PatientVitals;
using Microsoft.Data.SqlClient;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
namespace MicroEMR.Api.Controllers;
[ApiController]
[Authorize]
[RequirePermission(PermissionKeys.PatientsView)]
public sealed class PatientVitalsController(IPatientVitalService service, ILogger<PatientVitalsController> logger) : ControllerBase
{
    [HttpGet("api/patients/{patientUid:guid}/vitals")]
    public async Task<ActionResult<IReadOnlyList<PatientVitalResponse>>> GetByPatient(Guid patientUid,CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty) return BadRequest();
        try { return Ok(await service.GetByPatientUidAsync(patientUid,cancellationToken)); }
        catch (SqlException ex) when (ex.Number == 51401) { return NotFound(); }
    }
    [HttpGet("api/patients/{patientUid:guid}/vitals/{patientVitalUid:guid}")]
    public async Task<ActionResult<PatientVitalResponse>> GetByUid(Guid patientUid,Guid patientVitalUid,CancellationToken cancellationToken)
    {
        if(patientUid==Guid.Empty||patientVitalUid==Guid.Empty)return BadRequest();
        try
        {
            var result=await service.GetByUidAsync(patientUid,patientVitalUid,cancellationToken);
            return result is null?NotFound():Ok(result);
        }
        catch (SqlException ex) when (ex.Number == 51401) { return NotFound(); }
    }
    [HttpPost("api/patients/{patientUid:guid}/vitals"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public async Task<ActionResult<PatientVitalResponse>> Create(Guid patientUid,[FromBody]CreatePatientVitalRequest request,CancellationToken cancellationToken)
    {
        if(patientUid==Guid.Empty)return BadRequest();
        if(request.RecordedAt == default) ModelState.AddModelError(nameof(request.RecordedAt), "Recorded date and time are required.");
        if(!ModelState.IsValid)return ValidationProblem(ModelState);
        try { var result=await service.CreateAsync(patientUid,request,UserId(),cancellationToken); return result is null?NotFound():CreatedAtAction(nameof(GetByUid),new{patientUid,patientVitalUid=result.PatientVitalUid},result); }
        catch(SqlException ex) when(ex.Number == 51401){return NotFound();}
        catch(SqlException ex) when(ex.Number == 51403){return BadRequest(new{message="One or more vital measurements are invalid."});}
        catch(Exception ex){logger.LogError(ex,"Failed to create patient vitals.");return Problem("Vitals could not be saved.");}
    }
    [HttpPut("api/patients/{patientUid:guid}/vitals/{patientVitalUid:guid}"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public async Task<ActionResult<PatientVitalResponse>> Update(Guid patientUid,Guid patientVitalUid,[FromBody]UpdatePatientVitalRequest request,CancellationToken cancellationToken)
    {
        if(patientUid==Guid.Empty||patientVitalUid==Guid.Empty)return BadRequest();
        if(request.RecordedAt == default) ModelState.AddModelError(nameof(request.RecordedAt), "Recorded date and time are required.");
        if(!ModelState.IsValid)return ValidationProblem(ModelState);
        try { var result=await service.UpdateAsync(patientUid,patientVitalUid,request,UserId(),cancellationToken); return result is null?NotFound():Ok(result); }
        catch(FormatException){return BadRequest(new{message="The row version is invalid."});}
        catch(PatientVitalConcurrencyException){return Conflict(new{message="The vital record was changed by another user. Reload and try again."});}
        catch(SqlException ex) when(ex.Number == 51401){return NotFound();}
        catch(SqlException ex) when(ex.Number == 51403){return BadRequest(new{message="One or more vital measurements are invalid."});}
        catch(Exception ex){logger.LogError(ex,"Failed to update patient vitals.");return Problem("Vitals could not be saved.");}
    }
    private long UserId()=>ClinicalUserActorContext.GetRequired(HttpContext);
}
