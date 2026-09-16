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
public sealed class PatientPrescriptionsController(IPatientPrescriptionApiClient api):Controller
{
 [HttpGet,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)] public IActionResult Create(Guid patientUid)=>View(new CreatePrescriptionDraftRequest{PrescribedDate=DateOnly.FromDateTime(DateTime.Today)});
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)] public async Task<IActionResult> Create(Guid patientUid,CreatePrescriptionDraftRequest model,CancellationToken t)
 {
  var modalRequest=Request.Headers["X-Requested-With"]=="XMLHttpRequest";
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
 [HttpGet,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)] public async Task<IActionResult> Edit(Guid patientUid,Guid prescriptionUid,CancellationToken t){var x=await api.GetAsync(patientUid,prescriptionUid,t);if(x is null)return NotFound();if(x.Status!=PrescriptionStatuses.Draft)return Conflict();var model=new PrescriptionDraftRequest{ProductName=x.ProductName,ProductIdentifierNamespace=x.ProductIdentifierNamespace,ProductIdentifierValue=x.ProductIdentifierValue,ProductDisplayText=x.ProductDisplayText,StrengthValue=x.StrengthValue,StrengthUnit=x.StrengthUnit,DoseAmount=x.DoseAmount,DoseUnit=x.DoseUnit,Route=x.Route,FrequencyCode=x.FrequencyCode,Prn=x.Prn,Directions=x.Directions,Quantity=x.Quantity,QuantityUnit=x.QuantityUnit,AuthorizedRepeats=x.AuthorizedRepeats,Indication=x.Indication,PrescribedDate=x.PrescribedDate,StartDate=x.StartDate,RowVersion=x.RowVersion};return IsModalRequest()?Json(model):View(model);}
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)] public async Task<IActionResult> Edit(Guid patientUid,Guid prescriptionUid,PrescriptionDraftRequest model,CancellationToken t){if(!ModelState.IsValid)return IsModalRequest()?BadRequest(new{errors=ModelState.Values.SelectMany(x=>x.Errors).Select(x=>x.ErrorMessage).Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray()}):View(model);try{var updated=await api.UpdateAsync(patientUid,prescriptionUid,model,t);if(updated is null)return NotFound();TempData["SuccessMessage"]="Prescription draft updated.";return IsModalRequest()?Json(new{success=true,redirectUrl=Url.Action("Details","Patients",new{patientUid,tab="medications"})}):RedirectToAction("Details","Patients",new{patientUid,tab="medications"});}catch(HttpRequestException e){var message=e.StatusCode==System.Net.HttpStatusCode.Conflict?"The prescription changed. Reload the chart and try again.":GetErrorMessage(e);if(IsModalRequest())return BadRequest(new{errors=new[]{message}});ModelState.AddModelError("",message);return View(model);}}
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)] public async Task<IActionResult> Finalize(Guid patientUid,Guid prescriptionUid,string rowVersion,CancellationToken t){try{await api.ActionAsync(patientUid,prescriptionUid,"finalize",new(){RowVersion=rowVersion},t);TempData["SuccessMessage"]="Prescription finalized.";}catch(HttpRequestException e){TempData["ErrorMessage"]=GetErrorMessage(e);}return RedirectToAction("Details","Patients",new{patientUid,tab="medications"});}
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)] public async Task<IActionResult> Cancel(Guid patientUid,Guid prescriptionUid,string rowVersion,string? reason,CancellationToken t){await api.ActionAsync(patientUid,prescriptionUid,"cancel",new(){RowVersion=rowVersion,Reason=reason},t);return RedirectToAction("Details","Patients",new{patientUid,tab="medications"});}
 [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)] public async Task<IActionResult> Correction(Guid patientUid,Guid prescriptionUid,string rowVersion,CancellationToken t){try{var x=await api.ActionAsync(patientUid,prescriptionUid,"correction",new(){RowVersion=rowVersion},t);return x is null?NotFound():RedirectToAction(nameof(Edit),new{patientUid,prescriptionUid=x.PrescriptionUid});}catch(HttpRequestException e){TempData["ErrorMessage"]=GetErrorMessage(e);return RedirectToAction("Details","Patients",new{patientUid,tab="medications"});}}
 [HttpGet] public async Task<IActionResult> Artifact(Guid patientUid,Guid prescriptionUid,CancellationToken t){var x=await api.ArtifactAsync(patientUid,prescriptionUid,t);return x is null?NotFound():File(x,"application/pdf",$"prescription-{prescriptionUid:N}.pdf");}
 private static string GetErrorMessage(HttpRequestException exception)
 {
  var body=SafeApiResponseException.ValidationBody(exception);
  if(body.Length is >0 and <2048)
  {
   try
   {
    using var document=JsonDocument.Parse(body);
    if(document.RootElement.TryGetProperty("code",out var code) && code.GetString()=="provider_mapping_required")
     return "Your account must be linked to an active Provider before you can prescribe. Ask a clinic administrator to link your user in Providers.";
   }
   catch(JsonException) { }
  }
  return "The prescription could not be saved. Please try again or contact your clinic administrator.";
 }
 private bool IsModalRequest()=>Request.Headers["X-Requested-With"]=="XMLHttpRequest";
}
