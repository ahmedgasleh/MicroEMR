using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Cdm;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.Cdm;

namespace MicroEMR.Web.Controllers;

[Authorize,RequireWebPermission(PermissionKeys.PatientsView),Route("patients/{patientUid:guid}/cdm")]
public sealed class PatientCdmController(ICdmApiClient client):Controller
{
    [HttpGet] public async Task<IActionResult> Summary(Guid patientUid,CancellationToken token)=>Json(await client.Summary(patientUid,token));
    [HttpPost("enroll"),ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public async Task<IActionResult> Enroll(Guid patientUid,CreateCdmEnrollmentRequest request,CancellationToken token)=>Json(new{success=true,item=await client.Enroll(patientUid,request,token)});
    [HttpPost("{enrollmentUid:guid}/inactivate"),ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public async Task<IActionResult> Inactivate(Guid patientUid,Guid enrollmentUid,InactivateCdmEnrollmentRequest request,CancellationToken token)=>Json(new{success=true,item=await client.Inactivate(patientUid,enrollmentUid,request,token)});
}
