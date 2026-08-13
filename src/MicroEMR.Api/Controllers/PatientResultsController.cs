using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientResults;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/patients/{patientUid:guid}/results")]
[RequirePermission(PermissionKeys.ResultsView)]
public sealed class PatientResultsController(IPatientResultRepository repository, ILogger<PatientResultsController> logger) : ControllerBase
{
    [HttpGet("~/api/results/unreviewed-count")]
    public async Task<IActionResult> UnreviewedCount(CancellationToken token) =>
        Ok(new { count = await repository.GetUnreviewedCount(token) });

    [HttpGet] public async Task<IActionResult> List(Guid patientUid,string status="New",CancellationToken token=default)=>Ok(await repository.List(patientUid,status,token));
    [HttpGet("{uid:guid}")] public async Task<IActionResult> Get(Guid patientUid,Guid uid,CancellationToken token)=>await repository.Get(patientUid,uid,token)is{}result?Ok(result):NotFound();
    [HttpPost, RequirePermission(PermissionKeys.ResultsReview)] public Task<IActionResult> Create(Guid patientUid,CreatePatientResultRequest request,CancellationToken token)=>Mutate(()=>repository.Create(patientUid,request,UserId(),token),true);
    [HttpPut("{uid:guid}"), RequirePermission(PermissionKeys.ResultsReview)] public Task<IActionResult> Update(Guid patientUid,Guid uid,UpdatePatientResultRequest request,CancellationToken token)=>Mutate(()=>repository.Update(patientUid,uid,request,UserId(),token));
    [HttpPost("{uid:guid}/mark-reviewed"), RequirePermission(PermissionKeys.ResultsReview)] public Task<IActionResult> Review(Guid patientUid,Guid uid,MarkPatientResultReviewedRequest request,CancellationToken token)=>Mutate(()=>repository.Review(patientUid,uid,request,UserId(),token));

    private async Task<IActionResult> Mutate(Func<Task<PatientResultResponse?>> action,bool created=false)
    {
        try
        {
            var result=await action();
            return result is null?NotFound():created?CreatedAtAction(nameof(Get),new{patientUid=result.PatientUid,uid=result.PatientResultUid},result):Ok(result);
        }
        catch(SqlException exception)when(exception.Number==51302){return Conflict(new{message="Reviewed results cannot be edited."});}
        catch(Exception exception){logger.LogError(exception,"Patient result operation failed.");return StatusCode(500,new{message="The result operation could not be completed."});}
    }
    private long UserId()=>ClinicalUserActorContext.GetRequired(HttpContext);
}
