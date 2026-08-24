using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models.PatientImmunizations;
using MicroEMR.Web.Services.PatientImmunizations;

namespace MicroEMR.Web.Controllers;

[Authorize,RequireWebPermission(PermissionKeys.PatientsView),Route("patients/{patientUid:guid}/immunizations")]
public sealed class PatientImmunizationsController(IPatientImmunizationApiClient client,ILogger<PatientImmunizationsController> logger):Controller
{
    [HttpGet]public async Task<IActionResult>List(Guid patientUid,string status="All",CancellationToken token=default){try{return Json(new{success=true,items=await client.ListAsync(patientUid,status,token)});}catch(Exception e){return Failure(e,"Immunizations could not be loaded.");}}
    [HttpPost,ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ClinicalDataManage)]public async Task<IActionResult>Create(Guid patientUid,SavePatientImmunizationViewModel model,CancellationToken token){model.PatientUid=patientUid;if(!ModelState.IsValid)return BadRequest(new{success=false,message=Error()});try{return Json(new{success=true,item=await client.CreateAsync(patientUid,model,token)});}catch(Exception e){return Failure(e,"Immunization could not be saved.");}}
    [HttpPost("{immunizationUid:guid}"),ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ClinicalDataManage)]public async Task<IActionResult>Update(Guid patientUid,Guid immunizationUid,SavePatientImmunizationViewModel model,CancellationToken token){model.PatientUid=patientUid;model.ImmunizationUid=immunizationUid;if(!ModelState.IsValid)return BadRequest(new{success=false,message=Error()});try{return await client.UpdateAsync(patientUid,immunizationUid,model,token)is{}item?Json(new{success=true,item}):NotFound(new{success=false,message="Immunization was not found."});}catch(Exception e){return Failure(e,"Immunization could not be saved.");}}
    [HttpPost("{immunizationUid:guid}/entered-in-error"),ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ClinicalDataManage)]public async Task<IActionResult>MarkEnteredInError(Guid patientUid,Guid immunizationUid,MarkImmunizationEnteredInErrorViewModel model,CancellationToken token){model.PatientUid=patientUid;model.ImmunizationUid=immunizationUid;if(!ModelState.IsValid)return BadRequest(new{success=false,message=Error()});try{return await client.MarkEnteredInErrorAsync(patientUid,immunizationUid,model,token)is{}item?Json(new{success=true,item}):NotFound(new{success=false,message="Immunization was not found."});}catch(Exception e){return Failure(e,"Immunization could not be marked entered in error.");}}
    private IActionResult Failure(Exception e,string fallback){if(e is HttpRequestException h&&h.StatusCode==HttpStatusCode.Conflict)return Conflict(new{success=false,message=e.Message});logger.LogError(e,"Patient immunization request failed.");return StatusCode(StatusCodes.Status502BadGateway,new{success=false,message=fallback});}
    private string Error()=>ModelState.Values.SelectMany(x=>x.Errors).Select(x=>x.ErrorMessage).FirstOrDefault()??"Immunization information is invalid.";
}
