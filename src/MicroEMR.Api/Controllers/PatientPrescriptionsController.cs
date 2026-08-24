using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientPrescriptions;

namespace MicroEMR.Api.Controllers;

[ApiController,Authorize,RequirePermission(PermissionKeys.PatientsView)]
public sealed class PatientPrescriptionsController(IPatientPrescriptionService service):ControllerBase
{
 [HttpGet("api/patients/{patientUid:guid}/prescriptions")]
 public async Task<ActionResult<IReadOnlyList<PatientPrescriptionResponse>>> List(Guid patientUid,CancellationToken token)=>Ok(await service.ListAsync(patientUid,token));
 [HttpGet("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}")]
 public async Task<ActionResult<PatientPrescriptionResponse>> Get(Guid patientUid,Guid prescriptionUid,CancellationToken token){var x=await service.GetAsync(patientUid,prescriptionUid,token);return x is null?NotFound():Ok(x);}
 [HttpPost("api/patients/{patientUid:guid}/prescriptions"),RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
 public async Task<ActionResult<PatientPrescriptionResponse>> Create(Guid patientUid,[FromBody]CreatePrescriptionDraftRequest request,CancellationToken token){if(!ModelState.IsValid)return ValidationProblem(ModelState);try{var x=await service.CreateAsync(patientUid,request,Actor(),token);return CreatedAtAction(nameof(Get),new{patientUid,prescriptionUid=x.PrescriptionUid},x);}catch(InvalidOperationException e){return BadRequest(new{message=e.Message});}}
 [HttpPut("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}"),RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
 public async Task<ActionResult<PatientPrescriptionResponse>> Update(Guid patientUid,Guid prescriptionUid,[FromBody]PrescriptionDraftRequest request,CancellationToken token){if(!ModelState.IsValid)return ValidationProblem(ModelState);try{var x=await service.UpdateAsync(patientUid,prescriptionUid,request,Actor(),token);return x is null?NotFound():Ok(x);}catch(PatientPrescriptionConcurrencyException e){return Conflict(new{message=e.Message});}catch(FormatException){return BadRequest(new{message="RowVersion is invalid."});}}
 [HttpPost("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}/finalize"),RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
 public async Task<ActionResult<PatientPrescriptionResponse>> Finalize(Guid patientUid,Guid prescriptionUid,[FromBody]PrescriptionTransitionRequest request,CancellationToken token){try{var x=await service.FinalizeAsync(patientUid,prescriptionUid,request.RowVersion,Actor(),token);return x is null?NotFound():Ok(x);}catch(PatientPrescriptionConcurrencyException e){return Conflict(new{message=e.Message});}}
 [HttpPost("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}/cancel"),RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
 public async Task<ActionResult<PatientPrescriptionResponse>> Cancel(Guid patientUid,Guid prescriptionUid,[FromBody]PrescriptionTransitionRequest request,CancellationToken token){try{var x=await service.CancelAsync(patientUid,prescriptionUid,request.RowVersion,request.Reason,Actor(),token);return x is null?NotFound():Ok(x);}catch(PatientPrescriptionConcurrencyException e){return Conflict(new{message=e.Message});}}
 [HttpPost("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}/correction"),RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
 public async Task<ActionResult<PatientPrescriptionResponse>> Correction(Guid patientUid,Guid prescriptionUid,[FromBody]PrescriptionTransitionRequest request,CancellationToken token){var original=await service.GetAsync(patientUid,prescriptionUid,token);if(original is null)return NotFound();if(original.Status!=PrescriptionStatuses.Finalized||original.RowVersion!=request.RowVersion)return Conflict(new{message="Only the current finalized prescription can be corrected."});var draft=new CreatePrescriptionDraftRequest{ProductName=original.ProductName,ProductIdentifierNamespace=original.ProductIdentifierNamespace,ProductIdentifierValue=original.ProductIdentifierValue,ProductDisplayText=original.ProductDisplayText,StrengthValue=original.StrengthValue,StrengthUnit=original.StrengthUnit,DoseAmount=original.DoseAmount,DoseUnit=original.DoseUnit,Route=original.Route,FrequencyCode=original.FrequencyCode,Prn=original.Prn,Directions=original.Directions,Quantity=original.Quantity,QuantityUnit=original.QuantityUnit,AuthorizedRepeats=original.AuthorizedRepeats,Indication=original.Indication,PrescribedDate=DateOnly.FromDateTime(DateTime.UtcNow),StartDate=original.StartDate,SupersedesPrescriptionUid=original.PrescriptionUid};var x=await service.CreateAsync(patientUid,draft,Actor(),token);return CreatedAtAction(nameof(Get),new{patientUid,prescriptionUid=x.PrescriptionUid},x);}
 [HttpGet("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}/artifact")]
 public async Task<IActionResult> Artifact(Guid patientUid,Guid prescriptionUid,CancellationToken token){var bytes=await service.RenderArtifactPdfAsync(patientUid,prescriptionUid,token);return bytes is null?NotFound():File(bytes,"application/pdf",$"prescription-{prescriptionUid:N}.pdf");}
 private long Actor()=>ClinicalUserActorContext.GetRequired(HttpContext);
}
