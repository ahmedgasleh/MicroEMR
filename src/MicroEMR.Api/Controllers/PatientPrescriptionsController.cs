using System.Security.Claims;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.ClinicalUsers;
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
 public async Task<ActionResult<IReadOnlyList<PatientPrescriptionResponse>>> List(Guid patientUid,[FromServices]IPatientChartReadAuditService readAudit,CancellationToken token)
 {
  var rows=await service.ListAsync(patientUid,token);
  if(rows.Any(x=>x.PatientUid!=patientUid))return Forbid();
  try { await readAudit.RecordOpenedAsync(patientUid,HttpContext.TraceIdentifier,token); }
  catch(ClinicalUserResolutionException) { return Forbid(); }
  return Ok(rows);
 }
 [HttpGet("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}")]
 public async Task<ActionResult<PatientPrescriptionResponse>> Get(Guid patientUid,Guid prescriptionUid,[FromServices]IPatientChartReadAuditService readAudit,CancellationToken token)
 {
  var x=await service.GetAsync(patientUid,prescriptionUid,token);
  if(x is null)return NotFound();
  if(x.PatientUid!=patientUid||x.PrescriptionUid!=prescriptionUid)return Forbid();
  try { await readAudit.RecordOpenedAsync(patientUid,HttpContext.TraceIdentifier,token); }
  catch(ClinicalUserResolutionException) { return Forbid(); }
  return Ok(x);
 }
 [HttpPost("api/patients/{patientUid:guid}/prescriptions"),RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
[MicroEMR.Application.SecurityAudit.SensitiveCapability(MicroEMR.Application.SecurityAudit.SecurityAuditCapabilities.PrescriptionPrivateNoteWrite)]
 public async Task<ActionResult<PatientPrescriptionResponse>> Create(Guid patientUid,[FromBody]CreatePrescriptionDraftRequest request,CancellationToken token){if(!ModelState.IsValid)return ValidationProblem(ModelState);try{var x=await service.CreateAsync(patientUid,request,Actor(),token);return CreatedAtAction(nameof(Get),new{patientUid,prescriptionUid=x.PrescriptionUid},x);}catch(PrescriberProviderMappingException e){return Conflict(new{code="provider_mapping_required",message=e.Message});}catch(PrescriptionPrivateNoteAccessException){return Forbid();}catch(ArgumentException){return BadRequest(new{message="The prescription request is invalid."});}}
 [HttpPut("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}"),RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
[MicroEMR.Application.SecurityAudit.SensitiveCapability(MicroEMR.Application.SecurityAudit.SecurityAuditCapabilities.PrescriptionPrivateNoteWrite)]
 public async Task<ActionResult<PatientPrescriptionResponse>> Update(Guid patientUid,Guid prescriptionUid,[FromBody]PrescriptionDraftRequest request,CancellationToken token){if(!ModelState.IsValid)return ValidationProblem(ModelState);try{var x=await service.UpdateAsync(patientUid,prescriptionUid,request,Actor(),token);return x is null?NotFound():Ok(x);}catch(PatientPrescriptionConcurrencyException){return Conflict(new{message="The prescription changed or is no longer available. Reload and try again."});}catch(PrescriberProviderMappingException){return Conflict(new{code="provider_mapping_required",message="An active linked provider is required to prescribe."});}catch(FormatException){return BadRequest(new{message="RowVersion is invalid."});}catch(PrescriptionPrivateNoteAccessException){return Forbid();}catch(ArgumentException){return BadRequest(new{message="The prescription request is invalid."});}}
 [HttpPost("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}/finalize"),RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
 public async Task<ActionResult<PatientPrescriptionResponse>> Finalize(Guid patientUid,Guid prescriptionUid,[FromBody]PrescriptionTransitionRequest request,CancellationToken token){try{var x=await service.FinalizeAsync(patientUid,prescriptionUid,request.RowVersion,Actor(),token);return x is null?NotFound():Ok(x);}catch(PrescriberProviderMappingException e){return Conflict(new{code="provider_mapping_required",message=e.Message});}catch(PatientPrescriptionConcurrencyException){return Conflict(new{message="The prescription changed or is no longer available. Reload and try again."});}}
 [HttpPost("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}/sign-and-finalize"),RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
 public async Task<ActionResult<PatientPrescriptionResponse>> SignAndFinalize(Guid patientUid,Guid prescriptionUid,[FromBody]PrescriptionSigningRequest request,[FromServices]PrescriptionOutputService output,CancellationToken token)
 {
  try { var result=await output.SignAndFinalizeAsync(patientUid,prescriptionUid,request,Actor(),token); return result is null?NotFound():Ok(result); }
  catch(ArgumentException){return BadRequest(new{message="Explicit signature confirmation and a valid RowVersion are required."});}
  catch(PrescriberProviderMappingException){return Conflict(new{code="provider_mapping_required",message="An active linked provider is required to sign."});}
  catch(PatientPrescriptionConcurrencyException){return Conflict(new{message="The prescription changed or is no longer available. Reload and try again."});}
 }
 [HttpPost("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}/cancel"),RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
 public async Task<ActionResult<PatientPrescriptionResponse>> Cancel(Guid patientUid,Guid prescriptionUid,[FromBody]PrescriptionTransitionRequest request,CancellationToken token){try{var x=await service.CancelAsync(patientUid,prescriptionUid,request.RowVersion,request.Reason,Actor(),token);return x is null?NotFound():Ok(x);}catch(PatientPrescriptionConcurrencyException){return Conflict(new{message="The prescription changed or is no longer available. Reload and try again."});}}
 [HttpPost("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}/correction"),RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
 public async Task<ActionResult<PatientPrescriptionResponse>> Correction(Guid patientUid,Guid prescriptionUid,[FromBody]PrescriptionTransitionRequest request,CancellationToken token){var original=await service.GetAsync(patientUid,prescriptionUid,token);if(original is null)return NotFound();if(original.PatientUid!=patientUid||original.PrescriptionUid!=prescriptionUid)return Forbid();if(original.Status!=PrescriptionStatuses.Finalized||original.RowVersion!=request.RowVersion)return Conflict(new{message="Only the current finalized prescription can be corrected."});var draft=new CreatePrescriptionDraftRequest{OrderFields=new(){SubstitutionProhibited=original.SubstitutionProhibited,TreatmentType=original.TreatmentType},GenericName=original.GenericName,MedicationForm=original.MedicationForm,NotesToPharmacist=original.NotesToPharmacist,ProductName=original.ProductName,ProductIdentifierNamespace=original.ProductIdentifierNamespace,ProductIdentifierValue=original.ProductIdentifierValue,ProductDisplayText=original.ProductDisplayText,StrengthValue=original.StrengthValue,StrengthUnit=original.StrengthUnit,DoseAmount=original.DoseAmount,DoseUnit=original.DoseUnit,Route=original.Route,FrequencyCode=original.FrequencyCode,Prn=original.Prn,Directions=original.Directions,Quantity=original.Quantity,QuantityUnit=original.QuantityUnit,InitialDaysSupply=original.InitialDaysSupply,RefillQuantity=original.RefillQuantity,RefillQuantityUnit=original.RefillQuantityUnit,RefillDaysSupply=original.RefillDaysSupply,AuthorizedRepeats=original.AuthorizedRepeats,Indication=original.Indication,DateFields=new PrescriptionDateFields{WrittenDate=PrescriptionDateValue.From(DateOnly.FromDateTime(DateTime.UtcNow)),StartDate=original.DateFields?.StartDate??PrescriptionDateValue.From(original.StartDate)},SupersedesPrescriptionUid=original.PrescriptionUid};try{var x=await service.CreateAsync(patientUid,draft,Actor(),token);return CreatedAtAction(nameof(Get),new{patientUid,prescriptionUid=x.PrescriptionUid},x);}catch(PrescriberProviderMappingException e){return Conflict(new{code="provider_mapping_required",message=e.Message});}}
 [HttpGet("api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}/artifact")]
 [ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
 public async Task<IActionResult> Artifact(Guid patientUid,Guid prescriptionUid,[FromServices]PrescriptionOutputService output,CancellationToken token){
 var subject=User.FindFirstValue("sub")??User.FindFirstValue(ClaimTypes.NameIdentifier)??throw new UnauthorizedAccessException();
 var requester=new PrescriptionOutputRequester(subject,User.Identity?.Name??User.FindFirstValue("name")??subject,HttpContext.TraceIdentifier);
 var bytes=await output.GenerateAsync(patientUid,prescriptionUid,requester,token);return bytes is null?NotFound():File(bytes,"application/pdf",$"prescription-{prescriptionUid:N}.pdf");}
 private long Actor()=>ClinicalUserActorContext.GetRequired(HttpContext);
}
