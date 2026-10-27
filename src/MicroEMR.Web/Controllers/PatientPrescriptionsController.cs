using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.PatientPrescriptions;
using MicroEMR.Web.Services;

namespace MicroEMR.Web.Controllers;
[Authorize,RequireWebPermission(PermissionKeys.PatientsView)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PatientPrescriptionsController(IPatientPrescriptionApiClient api, IWebPermissionService? permissions=null):Controller
{
 [HttpGet,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)] public IActionResult Create(Guid patientUid)=>View(new CreatePrescriptionDraftRequest{PrescribedDate=DateOnly.FromDateTime(DateTime.Today)});
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)] public async Task<IActionResult> Create(Guid patientUid,CreatePrescriptionDraftRequest model,CancellationToken t)
 {
  BindPickup(model);BindFacilityName(model);BindFacilityAddress(model);BindReasonCoding(model);BindProtocolReference(model); var modalRequest=Request.Headers["X-Requested-With"]=="XMLHttpRequest";
  if(!ModelState.IsValid)
   return modalRequest?BadRequest(new{errors=ModelState.Values.SelectMany(x=>x.Errors).Select(x=>x.ErrorMessage).Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray()}):View(model);
  try
  {
   await api.CreateAsync(patientUid,model,t);
   TempData["SuccessMessage"]="Prescription draft created.";
   return modalRequest?Json(new{success=true,redirectUrl=Url.Action("Details","Patients",new{patientUid,tab="medications"})}):RedirectToAction("Details","Patients",new{patientUid,tab="medications"});
  }
  catch(HttpRequestException e)
  {
   var message=GetErrorMessage(e);
   if(modalRequest)return BadRequest(new{errors=new[]{message}});
   ModelState.AddModelError("",message);
   return View(model);
  }
 }
 [HttpGet,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)] public async Task<IActionResult> Edit(Guid patientUid,Guid prescriptionUid,CancellationToken t){var x=await api.GetAsync(patientUid,prescriptionUid,t);if(x is null)return NotFound();if(x.Status!=PrescriptionStatuses.Draft)return Conflict();var model=new PrescriptionDraftRequest{ProtocolReference=x.ProtocolReference,ProtocolReferenceReviewed=x.ProtocolReferenceReviewed,ProtocolReferenceReviewRequired=x.ProtocolReferenceReviewRequired,ReasonCoding=x.ReasonCoding,ReasonCodingReviewed=x.ReasonCodingReviewed,ReasonCodingReviewRequired=x.ReasonCodingReviewRequired,TargetDispensingFacilityAddress=x.TargetDispensingFacilityAddress,FacilityAddressReviewed=x.FacilityAddressReviewed,FacilityAddressReviewRequired=x.FacilityAddressReviewRequired,TargetDispensingFacilityName=x.TargetDispensingFacilityName,FacilityNameReviewed=x.FacilityNameReviewed,FacilityNameReviewRequired=x.FacilityNameReviewRequired,IsCorrectionDraft=x.SupersedesPrescriptionUid.HasValue,EarliestPickup=x.EarliestPickup,OrderFields=new(){PaperCopyRequiredBeforeDispense=x.PaperCopyRequiredBeforeDispense,MinimumDispenseIntervalDays=x.MinimumDispenseIntervalDays,SubstitutionProhibited=x.SubstitutionProhibited,TreatmentType=x.TreatmentType},GenericName=x.GenericName,MedicationForm=x.MedicationForm,NotesToPharmacist=x.NotesToPharmacist,ProductName=x.ProductName,ProductIdentifierNamespace=x.ProductIdentifierNamespace,ProductIdentifierValue=x.ProductIdentifierValue,ProductDisplayText=x.ProductDisplayText,StrengthValue=x.StrengthValue,StrengthUnit=x.StrengthUnit,DoseAmount=x.DoseAmount,DoseUnit=x.DoseUnit,Route=x.Route,FrequencyCode=x.FrequencyCode,Prn=x.Prn,Directions=x.Directions,Quantity=x.Quantity,QuantityUnit=x.QuantityUnit,InitialDaysSupply=x.InitialDaysSupply,RefillQuantity=x.RefillQuantity,RefillQuantityUnit=x.RefillQuantityUnit,RefillDaysSupply=x.RefillDaysSupply,AuthorizedRepeats=x.AuthorizedRepeats,Indication=x.Indication,DateFields=x.DateFields??PrescriptionDateFields.From(x.PrescribedDate,x.StartDate),RowVersion=x.RowVersion};if(!IsModalRequest() && permissions is not null && await permissions.HasAsync(PermissionKeys.PrescriptionsViewPrivateNotes,t)){var note=await api.PrivateNoteAsync(patientUid,prescriptionUid,t);ViewData["CanEditPrivateNote"]=note?.CanEdit==true;if(note?.CanEdit==true){if(model.RowVersion!=note.RowVersion)return Conflict("The prescription changed. Reload and try again.");model.PrivateNote=note.NoteText;}}return IsModalRequest()?Json(model):View(model);}
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)] public async Task<IActionResult> Edit(Guid patientUid,Guid prescriptionUid,PrescriptionDraftRequest model,CancellationToken t){BindPickup(model);BindFacilityName(model);BindFacilityAddress(model);BindReasonCoding(model);BindProtocolReference(model);await PreparePrivateEditorAsync(patientUid,prescriptionUid,t);if(!ModelState.IsValid)return IsModalRequest()?BadRequest(new{errors=ModelState.Values.SelectMany(x=>x.Errors).Select(x=>x.ErrorMessage).Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray()}):View(model);try{var updated=await api.UpdateAsync(patientUid,prescriptionUid,model,t);if(updated is null)return NotFound();TempData["SuccessMessage"]="Prescription draft updated.";return IsModalRequest()?Json(new{success=true,redirectUrl=Url.Action("Details","Patients",new{patientUid,tab="medications"})}):RedirectToAction("Details","Patients",new{patientUid,tab="medications"});}catch(HttpRequestException e){var message=e.StatusCode==System.Net.HttpStatusCode.Conflict?"The prescription changed. Reload the chart and try again.":GetErrorMessage(e);if(IsModalRequest())return BadRequest(new{errors=new[]{message}});ModelState.AddModelError("",message);return View(model);}}
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)] public async Task<IActionResult> Finalize(Guid patientUid,Guid prescriptionUid,string rowVersion,bool pickupReviewed,CancellationToken t){try{await api.ActionAsync(patientUid,prescriptionUid,"finalize",new(){RowVersion=rowVersion,PickupReviewed=pickupReviewed},t);TempData["SuccessMessage"]="Prescription finalized.";}catch(HttpRequestException e){TempData["ErrorMessage"]=GetErrorMessage(e);}return RedirectToAction("Details","Patients",new{patientUid,tab="medications"});}
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)]
 public async Task<IActionResult> SignAndFinalize(Guid patientUid,Guid prescriptionUid,string rowVersion,bool confirmSignature,bool pickupReviewed,CancellationToken t)
 {
  if(!confirmSignature)return BadRequest("Explicit signature confirmation is required.");
  try { await api.SignAndFinalizeAsync(patientUid,prescriptionUid,new(){RowVersion=rowVersion,ConfirmSignature=true,PickupReviewed=pickupReviewed},t);TempData["SuccessMessage"]="Prescription electronically signed and finalized."; }
  catch(HttpRequestException e){TempData["ErrorMessage"]=GetErrorMessage(e);}
  return RedirectToAction("Details","Patients",new{patientUid,tab="medications"});
 }
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)] public async Task<IActionResult> Cancel(Guid patientUid,Guid prescriptionUid,string rowVersion,string? reason,CancellationToken t){await api.ActionAsync(patientUid,prescriptionUid,"cancel",new(){RowVersion=rowVersion,Reason=reason},t);return RedirectToAction("Details","Patients",new{patientUid,tab="medications"});}
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)] public async Task<IActionResult> Correction(Guid patientUid,Guid prescriptionUid,string rowVersion,CancellationToken t){try{var x=await api.ActionAsync(patientUid,prescriptionUid,"correction",new(){RowVersion=rowVersion},t);return x is null?NotFound():RedirectToAction(nameof(Edit),new{patientUid,prescriptionUid=x.PrescriptionUid});}catch(HttpRequestException e){TempData["ErrorMessage"]=GetErrorMessage(e);return RedirectToAction("Details","Patients",new{patientUid,tab="medications"});}}
 [HttpGet,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)] public async Task<IActionResult> Artifact(Guid patientUid,Guid prescriptionUid,CancellationToken t){var x=await api.ArtifactAsync(patientUid,prescriptionUid,t);return x is null?NotFound():File(x,"application/pdf",$"prescription-{prescriptionUid:N}.pdf");}
 [HttpGet,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
 public async Task<IActionResult> Detail(Guid patientUid,Guid prescriptionUid,CancellationToken t)
 {
  var prescription=await api.GetAsync(patientUid,prescriptionUid,t);
  if(prescription is null)return NotFound();
  PrescriptionPrivateNoteDetail? note=null;
  IReadOnlyList<PrescriptionPrivateNoteRevision> history=[];
  if(permissions is not null && await permissions.HasAsync(PermissionKeys.PrescriptionsViewPrivateNotes,t))
  {
   note=await api.PrivateNoteAsync(patientUid,prescriptionUid,t);
   if(note is null)return NotFound();
   history=await api.PrivateNoteHistoryAsync(patientUid,prescriptionUid,t);
  }
  return View(new MicroEMR.Web.Models.PrescriptionDetailViewModel(prescription,note,history));
 }
 [HttpGet,RequireWebPermission(PermissionKeys.PrescriptionsViewPrivateNotes),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
 [MicroEMR.Application.SecurityAudit.SensitiveCapability(MicroEMR.Application.SecurityAudit.SecurityAuditCapabilities.PrescriptionPrivateNoteView)]
 public async Task<IActionResult> PrivateNoteEditor(Guid patientUid,Guid prescriptionUid,CancellationToken t)
 {
  var note=await api.PrivateNoteAsync(patientUid,prescriptionUid,t);
  return note is null?NotFound():Json(note.CanEdit?note:note with {NoteText=null});
 }
 private async Task PreparePrivateEditorAsync(Guid patientUid,Guid prescriptionUid,CancellationToken token)
 {
  ViewData["CanEditPrivateNote"]=false;
  if(permissions is null || !await permissions.HasAsync(PermissionKeys.PrescriptionsViewPrivateNotes,token))return;
  try {ViewData["CanEditPrivateNote"]=(await api.PrivateNoteAsync(patientUid,prescriptionUid,token))?.CanEdit==true;}
  catch(HttpRequestException) { /* Keep the editor absent when protected access is unavailable. */ }
 }
 [HttpGet,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)]
 public async Task<IActionResult> PickupOptions(string? localTime,CancellationToken token)
 {
  try{return Json(await api.PickupOptionsAsync(localTime,token));}
  catch(HttpRequestException e){return BadRequest(new{message=GetErrorMessage(e)});}
 }
 private void BindProtocolReference(PrescriptionDraftRequest model)
 {
  if(!Request.HasFormContentType)return;
  var operation=Request.Form["protocolReferenceOperation"].ToString();ViewData["ProtocolReferenceOperation"]=operation;
  model.OmitProtocolReference();
  if(operation=="clear")model.ProtocolReference=null;
  else if(operation=="set")model.ProtocolReference=new(Request.Form["ProtocolSystem"].ToString(),Request.Form["ProtocolIdentifier"].ToString());
  else if(!string.IsNullOrEmpty(operation)&&operation!="preserve")ModelState.AddModelError("ProtocolReference","Invalid protocol reference operation.");
 }
 private void BindReasonCoding(PrescriptionDraftRequest model)
 {
  if(!Request.HasFormContentType)return;
  var operation=Request.Form["reasonCodingOperation"].ToString();ViewData["ReasonCodingOperation"]=operation;
  model.OmitReasonCoding();
  if(operation=="clear")model.ReasonCoding=null;
  else if(operation=="set")model.ReasonCoding=new(Request.Form["ReasonSystem"].ToString(),Request.Form["ReasonCode"].ToString(),Request.Form["ReasonVersion"].ToString(),Request.Form["ReasonDisplay"].ToString());
  else if(!string.IsNullOrEmpty(operation)&&operation!="preserve")ModelState.AddModelError("ReasonCoding","Invalid coded reason operation.");
 }
 private void BindFacilityAddress(PrescriptionDraftRequest model)
 {
  if(!Request.HasFormContentType)return;
  var operation=Request.Form["facilityAddressOperation"].ToString();
  ViewData["FacilityAddressOperation"]=operation;
  model.OmitTargetDispensingFacilityAddress();
  if(operation=="clear")model.TargetDispensingFacilityAddress=null;
  else if(operation=="set")model.TargetDispensingFacilityAddress=Request.Form["TargetDispensingFacilityAddress"].ToString();
  else if(!string.IsNullOrEmpty(operation)&&operation!="preserve")ModelState.AddModelError("TargetDispensingFacilityAddress","Invalid facility address operation.");
 }
 private void BindFacilityName(PrescriptionDraftRequest model)
 {
  if(!Request.HasFormContentType)return;
  var operation=Request.Form["facilityNameOperation"].ToString();
  ViewData["FacilityNameOperation"]=operation;
  model.OmitTargetDispensingFacilityName();
  if(operation=="clear")model.TargetDispensingFacilityName=null;
  else if(operation=="set")model.TargetDispensingFacilityName=Request.Form["TargetDispensingFacilityName"].ToString();
  else if(!string.IsNullOrEmpty(operation)&&operation!="preserve")ModelState.AddModelError("TargetDispensingFacilityName","Invalid facility name operation.");
 }
 private void BindPickup(PrescriptionDraftRequest model)
 {
  if(!Request.HasFormContentType)return;
  var operation=Request.Form["pickupOperation"].ToString();
  ViewData["PickupOperation"]=operation;
  model.OmitEarliestPickup();
  if(operation=="clear"){model.EarliestPickup=null;return;}
  if(string.IsNullOrEmpty(operation)||operation=="preserve")return;
  if(operation!="set"){ModelState.AddModelError("EarliestPickup","Invalid pickup operation.");return;}
  var precision=Request.Form["pickupPrecision"].ToString();
  var value=Request.Form["pickupDate"].ToString();
  if(precision=="Minute")value+="T"+Request.Form["pickupTime"]+Request.Form["pickupOffset"];
  var pickup=new EarliestPickup(precision,value,Request.Form["pickupZone"].ToString());
  try{pickup.ValidateShape();model.EarliestPickup=pickup;}
  catch(ArgumentException e){ModelState.AddModelError("EarliestPickup",e.Message);}
 }
 private static string GetErrorMessage(HttpRequestException exception)
 {
  var body=SafeApiResponseException.ValidationBody(exception);
  if(body.Length is >0 and <2048)
  {
   try
   {
    using var document=JsonDocument.Parse(body);
    if(document.RootElement.TryGetProperty("message",out var fieldMessage) && fieldMessage.GetString() is {} text && text.Length<300 && (text.Contains("pickup",StringComparison.OrdinalIgnoreCase)||text.Contains("timezone",StringComparison.OrdinalIgnoreCase)||text.Contains("facility",StringComparison.OrdinalIgnoreCase)||text.Contains("reason",StringComparison.OrdinalIgnoreCase)||text.Contains("protocol",StringComparison.OrdinalIgnoreCase)))return text;
    if(document.RootElement.TryGetProperty("code",out var code) && code.GetString()=="provider_mapping_required")
     return "Your account must be linked to an active Provider before you can prescribe. Ask a clinic administrator to link your user in Providers.";
   }
   catch(JsonException) { }
  }
  return "The prescription could not be saved. Please try again or contact your clinic administrator.";
 }
 private bool IsModalRequest()=>Request.Headers["X-Requested-With"]=="XMLHttpRequest";
}
