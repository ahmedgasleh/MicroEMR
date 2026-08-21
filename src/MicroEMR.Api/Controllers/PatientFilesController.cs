using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientFiles;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ReadAudit;
using MicroEMR.Application.SecurityAudit;

namespace MicroEMR.Api.Controllers;

[ApiController,Authorize,Route("api/patients/{patientUid:guid}/files")]
[RequirePermission(PermissionKeys.DocumentsView)]
public sealed class PatientFilesController(
    IPatientFileService service,
    IStructuredReadAuditService readAudit,
    ILogger<PatientFilesController> logger):ControllerBase
{
    [HttpGet]public async Task<ActionResult<IReadOnlyList<PatientFileResponse>>>List(Guid patientUid,CancellationToken ct)=>Ok(await service.GetByPatientUidAsync(patientUid,ct));
    [HttpGet("{fileUid:guid}")]public async Task<ActionResult<PatientFileResponse>>Get(Guid patientUid,Guid fileUid,CancellationToken ct)=>await service.GetByUidAsync(patientUid,fileUid,ct)is{}x?Ok(x):NotFound();
    [HttpGet("{fileUid:guid}/content")]
    [SensitiveCapability(SecurityAuditCapabilities.PatientFileDownload)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Content(Guid patientUid,Guid fileUid,CancellationToken ct)
    {
        var content=await service.OpenContentAsync(patientUid,fileUid,ct);
        if(content is null)return NotFound();
        try
        {
            await readAudit.RecordAsync(ReadAuditActions.PatientFileDownloaded,ReadAuditResourceTypes.PatientFile,
                content.FileUid,content.PatientUid,HttpContext.TraceIdentifier,ct);
        }
        catch(OperationCanceledException) when(HttpContext.RequestAborted.IsCancellationRequested)
        {
            await content.Content.DisposeAsync();
            throw;
        }
        catch(Exception exception)
        {
            await content.Content.DisposeAsync();
            logger.LogError(exception,
                "Patient file download audit failed for file {FileUid}; disclosure was prevented. TraceIdentifier: {TraceIdentifier}.",
                content.FileUid,HttpContext.TraceIdentifier);
            return Problem(statusCode:StatusCodes.Status503ServiceUnavailable,
                title:"Patient file download audit unavailable",
                detail:"The file cannot be downloaded because access auditing is temporarily unavailable.");
        }
        Response.Headers.XContentTypeOptions="nosniff";
        return File(content.Content,content.ContentType,content.FileName,enableRangeProcessing:true);
    }
    [HttpPost,RequestSizeLimit(27_262_976),RequirePermission(PermissionKeys.DocumentsManage)]
    public async Task<ActionResult<PatientFileResponse>>Upload(Guid patientUid,IFormFile? file,[FromForm]string? description,[FromForm]string? category,[FromForm]string? title,[FromForm]string? sourceOrganization,[FromForm]string? authorName,[FromForm]DateOnly? documentDate,[FromForm]DateOnly? receivedDate,CancellationToken ct)
    {
        if(file is null){ModelState.AddModelError("file","A file is required.");return ValidationProblem(ModelState);}
        try{await using var stream=file.OpenReadStream();var x=await service.UploadAsync(patientUid,new(stream,file.FileName,file.ContentType,file.Length,description,category,title,sourceOrganization,authorName,documentDate,receivedDate),ct);return CreatedAtAction(nameof(Get),new{patientUid,fileUid=x.FileUid},x);}
        catch(KeyNotFoundException){return NotFound();}catch(ArgumentException e){ModelState.AddModelError("file",e.Message);return ValidationProblem(ModelState);}
    }
    [HttpPost("{fileUid:guid}/archive"),RequirePermission(PermissionKeys.DocumentsManage)]
    public Task<ActionResult<PatientFileResponse>>Archive(Guid patientUid,Guid fileUid,PatientFileLifecycleRequest request,CancellationToken ct)=>Transition(patientUid,fileUid,request,(p,f,v,t)=>service.ArchiveAsync(p,f,v,t),ct);
    [HttpPost("{fileUid:guid}/restore"),RequirePermission(PermissionKeys.DocumentsManage)]
    public Task<ActionResult<PatientFileResponse>>Restore(Guid patientUid,Guid fileUid,PatientFileLifecycleRequest request,CancellationToken ct)=>Transition(patientUid,fileUid,request,(p,f,v,t)=>service.RestoreAsync(p,f,v,t),ct);
    private async Task<ActionResult<PatientFileResponse>>Transition(Guid p,Guid f,PatientFileLifecycleRequest r,Func<Guid,Guid,string,CancellationToken,Task<PatientFileResponse>> action,CancellationToken ct)
    {try{return Ok(await action(p,f,r.RowVersion,ct));}catch(KeyNotFoundException){return NotFound();}catch(PatientFileConcurrencyException){return Conflict(new{message="This file was changed by another user."});}catch(PatientFileInvalidTransitionException){return Conflict(new{message="The requested file status change is no longer available."});}catch(ArgumentException e){ModelState.AddModelError("rowVersion",e.Message);return ValidationProblem(ModelState);}}
}
