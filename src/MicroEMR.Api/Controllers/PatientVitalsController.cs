using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientVitals.Contracts;
using MicroEMR.Application.PatientVitals.Services;
namespace MicroEMR.Api.Controllers;
[ApiController]
[Authorize]
public sealed class PatientVitalsController(IPatientVitalService service, ILogger<PatientVitalsController> logger) : ControllerBase
{
    [HttpGet("api/patients/{patientUid:guid}/vitals")]
    public async Task<ActionResult<IReadOnlyList<PatientVitalResponse>>> GetByPatient(Guid patientUid,CancellationToken cancellationToken)
        => patientUid==Guid.Empty ? BadRequest() : Ok(await service.GetByPatientUidAsync(patientUid,cancellationToken));
    [HttpGet("api/patients/{patientUid:guid}/vitals/{patientVitalUid:guid}")]
    public async Task<ActionResult<PatientVitalResponse>> GetByUid(Guid patientUid,Guid patientVitalUid,CancellationToken cancellationToken)
    {
        if(patientUid==Guid.Empty||patientVitalUid==Guid.Empty)return BadRequest();
        var result=await service.GetByUidAsync(patientUid,patientVitalUid,cancellationToken);
        return result is null?NotFound():Ok(result);
    }
    [HttpPost("api/patients/{patientUid:guid}/vitals")]
    public async Task<ActionResult<PatientVitalResponse>> Create(Guid patientUid,[FromBody]CreatePatientVitalRequest request,CancellationToken cancellationToken)
    {
        if(patientUid==Guid.Empty)return BadRequest(); if(!ModelState.IsValid)return ValidationProblem(ModelState);
        try { var result=await service.CreateAsync(patientUid,request,UserId(),cancellationToken); return result is null?NotFound():CreatedAtAction(nameof(GetByUid),new{patientUid,patientVitalUid=result.PatientVitalUid},result); }
        catch(Exception ex){logger.LogError(ex,"Failed to create patient vitals.");return Problem("Vitals could not be saved.");}
    }
    [HttpPut("api/patients/{patientUid:guid}/vitals/{patientVitalUid:guid}")]
    public async Task<ActionResult<PatientVitalResponse>> Update(Guid patientUid,Guid patientVitalUid,[FromBody]UpdatePatientVitalRequest request,CancellationToken cancellationToken)
    {
        if(patientUid==Guid.Empty||patientVitalUid==Guid.Empty)return BadRequest(); if(!ModelState.IsValid)return ValidationProblem(ModelState);
        try { var result=await service.UpdateAsync(patientUid,patientVitalUid,request,UserId(),cancellationToken); return result is null?NotFound():Ok(result); }
        catch(Exception ex){logger.LogError(ex,"Failed to update patient vitals.");return Problem("Vitals could not be saved.");}
    }
    private long? UserId(){var value=User.FindFirstValue("user_id")??User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");return long.TryParse(value,out var id)?id:null;}
}
