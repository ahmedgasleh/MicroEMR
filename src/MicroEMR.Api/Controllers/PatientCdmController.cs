using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Cdm;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, RequirePermission(PermissionKeys.PatientsView)]
[Route("api/patients/{patientUid:guid}/cdm")]
public sealed class PatientCdmController(ICdmEnrollmentService service, ILogger<PatientCdmController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CdmSummaryResponse>> Summary(Guid patientUid, CancellationToken token) =>
        patientUid == Guid.Empty ? BadRequest() : Ok(await service.GetSummaryAsync(patientUid, token));

    [HttpGet("enrollments/{enrollmentUid:guid}")]
    public async Task<ActionResult<CdmEnrollmentResponse>> Get(Guid patientUid, Guid enrollmentUid, CancellationToken token) =>
        await service.GetAsync(patientUid, enrollmentUid, token) is { } item ? Ok(item) : NotFound();

    [HttpPost("enrollments"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public async Task<ActionResult<CdmEnrollmentResponse>> Enroll(Guid patientUid, CreateCdmEnrollmentRequest request, CancellationToken token)
    {
        try
        {
            var item=await service.CreateAsync(patientUid,request,ClinicalUserActorContext.GetRequired(HttpContext),token);
            return CreatedAtAction(nameof(Get),new{patientUid,enrollmentUid=item.ChronicDiseaseEnrollmentUid},item);
        }
        catch(CdmEnrollmentValidationException e){return BadRequest(new{message=e.Message});}
        catch(CdmEnrollmentConflictException e){return Conflict(new{message=e.Message});}
    }

    [HttpPost("enrollments/{enrollmentUid:guid}/inactivate"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public async Task<ActionResult<CdmEnrollmentResponse>> Inactivate(Guid patientUid, Guid enrollmentUid, InactivateCdmEnrollmentRequest request, CancellationToken token)
    {
        try{return await service.InactivateAsync(patientUid,enrollmentUid,request,ClinicalUserActorContext.GetRequired(HttpContext),token) is{} item?Ok(item):NotFound();}
        catch(CdmEnrollmentValidationException e){return BadRequest(new{message=e.Message});}
        catch(CdmEnrollmentConflictException e){return Conflict(new{message=e.Message});}
        catch(CdmEnrollmentConcurrencyException e){logger.LogWarning("Stale CDM enrollment inactivation rejected for {EnrollmentUid}.",enrollmentUid);return Conflict(new{message=e.Message});}
    }
}
