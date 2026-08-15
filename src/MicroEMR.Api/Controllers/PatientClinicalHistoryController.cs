using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientClinicalHistory;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/patients/{patientUid:guid}/clinical-history")]
[RequirePermission(PermissionKeys.PatientsView)]
public sealed class PatientClinicalHistoryController(IPatientClinicalHistoryService service) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<IReadOnlyList<PatientClinicalHistoryResponse>>> List(Guid patientUid,string status="Active",CancellationToken ct=default)=>Ok(await service.ListAsync(patientUid,status,ct));
    [HttpPost,RequirePermission(PermissionKeys.ClinicalDataManage)] public async Task<ActionResult<PatientClinicalHistoryResponse>> Create(Guid patientUid,CreatePatientClinicalHistoryRequest request,CancellationToken ct){var x=await service.CreateAsync(patientUid,request,Actor(),ct);return Created($"api/patients/{patientUid}/clinical-history/{x.HistoryUid}",x);}
    [HttpPut("{historyUid:guid}"),RequirePermission(PermissionKeys.ClinicalDataManage)] public async Task<ActionResult<PatientClinicalHistoryResponse>> Update(Guid patientUid,Guid historyUid,UpdatePatientClinicalHistoryRequest request,CancellationToken ct){try{return await service.UpdateAsync(patientUid,historyUid,request,Actor(),ct)is{}x?Ok(x):NotFound();}catch(PatientClinicalHistoryConcurrencyException){return Conflict(new{message="This history item was changed by another user."});}catch(PatientClinicalHistoryArchivedException){return Conflict(new{message="Archived history cannot be edited."});}}
    [HttpPost("{historyUid:guid}/archive"),RequirePermission(PermissionKeys.ClinicalDataManage)] public async Task<ActionResult<PatientClinicalHistoryResponse>> Archive(Guid patientUid,Guid historyUid,ArchivePatientClinicalHistoryRequest request,CancellationToken ct){try{return await service.ArchiveAsync(patientUid,historyUid,request.RowVersion,Actor(),ct)is{}x?Ok(x):NotFound();}catch(PatientClinicalHistoryConcurrencyException){return Conflict(new{message="This history item was changed by another user."});}catch(PatientClinicalHistoryArchivedException){return Conflict(new{message="History is already archived."});}}
    private long Actor()=>ClinicalUserActorContext.GetRequired(HttpContext);
}
