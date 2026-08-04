using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientFiles;

namespace MicroEMR.Api.Controllers;

[ApiController,Authorize,Route("api/patients/{patientUid:guid}/files")]
public sealed class PatientFilesController(IPatientFileService service):ControllerBase
{
    [HttpGet]public async Task<ActionResult<IReadOnlyList<PatientFileResponse>>>List(Guid patientUid,CancellationToken ct)=>Ok(await service.GetByPatientUidAsync(patientUid,ct));
    [HttpGet("{fileUid:guid}")]public async Task<ActionResult<PatientFileResponse>>Get(Guid patientUid,Guid fileUid,CancellationToken ct)=>await service.GetByUidAsync(patientUid,fileUid,ct)is{}x?Ok(x):NotFound();
    [HttpGet("{fileUid:guid}/content")]public async Task<IActionResult>Content(Guid patientUid,Guid fileUid,CancellationToken ct){var x=await service.OpenContentAsync(patientUid,fileUid,ct);if(x is null)return NotFound();Response.Headers.XContentTypeOptions="nosniff";return File(x.Content,x.ContentType,x.FileName,enableRangeProcessing:true);}
    [HttpPost,RequestSizeLimit(27_262_976)]
    public async Task<ActionResult<PatientFileResponse>>Upload(Guid patientUid,IFormFile? file,[FromForm]string? description,[FromForm]string? category,CancellationToken ct)
    {
        if(file is null){ModelState.AddModelError("file","A file is required.");return ValidationProblem(ModelState);}
        try{await using var stream=file.OpenReadStream();var x=await service.UploadAsync(patientUid,new(stream,file.FileName,file.ContentType,file.Length,description,category),ct);return CreatedAtAction(nameof(Get),new{patientUid,fileUid=x.FileUid},x);}
        catch(KeyNotFoundException){return NotFound();}catch(ArgumentException e){ModelState.AddModelError("file",e.Message);return ValidationProblem(ModelState);}
    }
    [HttpPost("{fileUid:guid}/archive")]
    public Task<ActionResult<PatientFileResponse>>Archive(Guid patientUid,Guid fileUid,PatientFileLifecycleRequest request,CancellationToken ct)=>Transition(patientUid,fileUid,request,(p,f,v,t)=>service.ArchiveAsync(p,f,v,t),ct);
    [HttpPost("{fileUid:guid}/restore")]
    public Task<ActionResult<PatientFileResponse>>Restore(Guid patientUid,Guid fileUid,PatientFileLifecycleRequest request,CancellationToken ct)=>Transition(patientUid,fileUid,request,(p,f,v,t)=>service.RestoreAsync(p,f,v,t),ct);
    private async Task<ActionResult<PatientFileResponse>>Transition(Guid p,Guid f,PatientFileLifecycleRequest r,Func<Guid,Guid,string,CancellationToken,Task<PatientFileResponse>> action,CancellationToken ct)
    {try{return Ok(await action(p,f,r.RowVersion,ct));}catch(KeyNotFoundException){return NotFound();}catch(PatientFileConcurrencyException){return Conflict(new{message="This file was changed by another user."});}catch(PatientFileInvalidTransitionException){return Conflict(new{message="The requested file status change is no longer available."});}catch(ArgumentException e){ModelState.AddModelError("rowVersion",e.Message);return ValidationProblem(ModelState);}}
}
