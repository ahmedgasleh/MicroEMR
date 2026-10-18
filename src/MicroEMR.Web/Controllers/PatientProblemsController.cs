using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientProblems.Contracts;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Models.PatientProblems;
using MicroEMR.Web.Services.PatientProblems;
using MicroEMR.Web.Services;
using CreateRequest = MicroEMR.Web.Models.PatientProblems.CreatePatientProblemRequest;
using UpdateRequest = MicroEMR.Web.Models.PatientProblems.UpdatePatientProblemRequest;
using ResolveRequest = MicroEMR.Web.Models.PatientProblems.ResolvePatientProblemRequest;

namespace MicroEMR.Web.Controllers;

[Authorize, RequireWebPermission(PermissionKeys.PatientsView)]
public sealed class PatientProblemsController(IPatientProblemApiClient client, ILogger<PatientProblemsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Problems(Guid patientUid, string status = "Active", CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty) return BadRequest();
        if (!new[]{"Active","Resolved","All"}.Contains(status,StringComparer.OrdinalIgnoreCase)) status="Active";
        try { return Json(await client.GetByPatientUidAsync(patientUid,status,cancellationToken)); }
        catch(Exception e) when(e is HttpRequestException or UnauthorizedAccessException) { return Failure(e); }
    }
    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public async Task<IActionResult> CreateProblem(CreatePatientProblemViewModel model,CancellationToken cancellationToken)
    {
        if(model.PatientUid==Guid.Empty || !ModelState.IsValid) return BadRequest(new{success=false,message=FirstError()});
        var request=new CreateRequest();Copy(model,request,model.ClinicalFieldsIncluded);
        try { return Json(new{success=true,message="Problem saved.",problem=await client.CreateAsync(model.PatientUid,request,cancellationToken)}); }
        catch(Exception e) when(e is HttpRequestException or UnauthorizedAccessException) { return Failure(e); }
    }
    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public async Task<IActionResult> UpdateProblem(UpdatePatientProblemViewModel model,CancellationToken cancellationToken)
    {
        if(model.PatientUid==Guid.Empty || model.PatientProblemUid==Guid.Empty || !ModelState.IsValid) return BadRequest(new{success=false,message=FirstError()});
        var request=new UpdateRequest{RowVersion=model.RowVersion};Copy(model,request,model.ClinicalFieldsIncluded);
        try
        {
            var row=await client.UpdateAsync(model.PatientUid,model.PatientProblemUid,request,cancellationToken);
            return row is null ? NotFound(new{success=false,message="Problem was not found."}) : Json(new{success=true,message="Problem saved.",problem=row});
        }
        catch(Exception e) when(e is HttpRequestException or UnauthorizedAccessException) { return Failure(e); }
    }
    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public async Task<IActionResult> ResolveProblem(ResolvePatientProblemViewModel model,CancellationToken cancellationToken)
    {
        if(model.PatientUid==Guid.Empty || model.PatientProblemUid==Guid.Empty || !ModelState.IsValid) return BadRequest(new{success=false,message=FirstError()});
        try
        {
            var row=await client.ResolveAsync(model.PatientUid,model.PatientProblemUid,new ResolveRequest{RowVersion=model.RowVersion,ResolutionReason=model.ResolutionReason,ClinicalResolutionDate=model.ClinicalResolutionDate},cancellationToken);
            return row is null ? NotFound(new{success=false,message="Problem was not found."}) : Json(new{success=true,message="Problem resolved.",problem=row});
        }
        catch(Exception e) when(e is HttpRequestException or UnauthorizedAccessException) { return Failure(e); }
    }
    private static void Copy(ProblemSaveRequest source,ProblemSaveRequest target,bool included)
    {
        target.ProblemName=source.ProblemName;target.ProblemDescription=source.ProblemDescription;
        target.ClinicalFields=included ? source.ClinicalFields ?? new() : source.ClinicalFields;
        if(source.OnsetDateSpecified) target.OnsetDate=source.OnsetDate;
    }
    private string FirstError()=>ModelState.Values.SelectMany(x=>x.Errors).Select(x=>x.ErrorMessage).FirstOrDefault() ?? "Problem information is invalid.";
    private ObjectResult Failure(Exception error)
    {
        var status=error is HttpRequestException request && request.StatusCode is {} code ? (int)code : error is UnauthorizedAccessException ? 401 : 502;
        var message=status==409 ? "This problem changed or is already resolved. Reload and review before saving again." : status==403 ? "You are not authorized to change Problems." : status==401 ? "Sign in again before continuing." : status==404 ? "Problem was not found." : "Problem could not be saved.";
        if(status==400)
        {
            try
            {
                using var body=JsonDocument.Parse(SafeApiResponseException.ValidationBody(error as HttpRequestException ?? new HttpRequestException()));
                if(body.RootElement.TryGetProperty("message",out var value) && value.ValueKind==JsonValueKind.String) message=value.GetString()!;
            }
            catch(JsonException) { }
        }
        if(status>=500) logger.LogError(error,"Patient problem API operation failed.");
        return StatusCode(status,new{success=false,message});
    }
}
