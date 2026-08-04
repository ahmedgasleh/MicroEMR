using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.PatientFiles;
using MicroEMR.Web.Services.PatientFiles;

namespace MicroEMR.Web.Controllers;

[Authorize]
[Route("patients/{patientUid:guid}/files")]
public sealed class PatientFilesController(IPatientFileApiClient client, ILogger<PatientFilesController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> List(Guid patientUid, CancellationToken cancellationToken)
    {
        try { return Json(new { success = true, files = await client.GetByPatientUidAsync(patientUid, cancellationToken) }); }
        catch (Exception exception) { return Failure(exception, "Files could not be loaded.", patientUid); }
    }

    [HttpGet("{fileUid:guid}")]
    public async Task<IActionResult> Details(Guid patientUid, Guid fileUid, CancellationToken cancellationToken)
    {
        try
        {
            var file = await client.GetByUidAsync(patientUid, fileUid, cancellationToken);
            return file is null ? NotFound(new { success = false, message = "File metadata was not found." }) : Json(new { success = true, file });
        }
        catch (Exception exception) { return Failure(exception, "File details could not be loaded.", patientUid, fileUid); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(27_262_976)]
    public async Task<IActionResult> Upload(Guid patientUid, UploadPatientFileViewModel model, CancellationToken cancellationToken)
    {
        if (model.File is null) ModelState.AddModelError(nameof(model.File), "A file is required.");
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = ModelState.Values.SelectMany(x => x.Errors).FirstOrDefault()?.ErrorMessage ?? "Please correct the file information." });
        try
        {
            var file = await client.UploadAsync(patientUid, model.File!, model.Description, model.Category, cancellationToken);
            return file is null ? NotFound(new { success = false, message = "Patient was not found." }) : Json(new { success = true, file, message = "File uploaded successfully." });
        }
        catch (Exception exception) { return Failure(exception, "The file could not be uploaded.", patientUid); }
    }
    [HttpPost("{fileUid:guid}/archive"),ValidateAntiForgeryToken]
    public Task<IActionResult>Archive(Guid patientUid,Guid fileUid,string rowVersion,CancellationToken ct)=>Transition(patientUid,fileUid,rowVersion,client.ArchiveAsync,ct);
    [HttpPost("{fileUid:guid}/restore"),ValidateAntiForgeryToken]
    public Task<IActionResult>Restore(Guid patientUid,Guid fileUid,string rowVersion,CancellationToken ct)=>Transition(patientUid,fileUid,rowVersion,client.RestoreAsync,ct);
    private async Task<IActionResult>Transition(Guid p,Guid f,string v,Func<Guid,Guid,string,CancellationToken,Task<PatientFileViewModel?>> action,CancellationToken ct)
    {if(string.IsNullOrWhiteSpace(v))return BadRequest(new{success=false,message="The file version is required."});try{var file=await action(p,f,v,ct);return file is null?NotFound(new{success=false,message="File was not found."}):Json(new{success=true,file});}catch(Exception e){return Failure(e,"The file status could not be changed.",p,f);}}

    [HttpGet("{fileUid:guid}/content")]
    public async Task<IActionResult> Content(Guid patientUid, Guid fileUid, CancellationToken cancellationToken)
    {
        HttpResponseMessage? apiResponse = null;
        try
        {
            apiResponse = await client.GetContentAsync(patientUid, fileUid, cancellationToken);
            if (apiResponse.StatusCode == HttpStatusCode.NotFound)
                return NotFound("The file content is currently unavailable.");

            Response.StatusCode = (int)apiResponse.StatusCode;
            Response.ContentType = apiResponse.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            if (apiResponse.Content.Headers.ContentLength is { } length) Response.ContentLength = length;
            if (apiResponse.Content.Headers.ContentDisposition is { } disposition)
                Response.Headers.ContentDisposition = disposition.ToString();
            Response.Headers.XContentTypeOptions = "nosniff";
            await using var stream = await apiResponse.Content.ReadAsStreamAsync(cancellationToken);
            await stream.CopyToAsync(Response.Body, cancellationToken);
            return new EmptyResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new EmptyResult(); }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Content proxy failed for file {FileUid}, patient {PatientUid}.", fileUid, patientUid);
            if (!Response.HasStarted && exception is HttpRequestException http)
                return http.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => Unauthorized("Your session is no longer authorized."),
                    HttpStatusCode.Forbidden => StatusCode(403, "You are not authorized to access this file."),
                    HttpStatusCode.NotFound => NotFound("The file content is currently unavailable."),
                    _ => StatusCode(502, "The file content is currently unavailable.")
                };
            if (!Response.HasStarted) return StatusCode(502, "The file content is currently unavailable.");
            return new EmptyResult();
        }
        finally { apiResponse?.Dispose(); }
    }

    private IActionResult Failure(Exception exception, string fallback, Guid patientUid, Guid? fileUid = null)
    {
        logger.LogWarning(exception, "Patient file request failed for patient {PatientUid}, file {FileUid}.", patientUid, fileUid);
        if (exception is UnauthorizedAccessException) return Unauthorized(new { success = false, message = "Your session is no longer authorized." });
        if (exception is HttpRequestException http)
            return StatusCode(http.StatusCode is null ? 502 : (int)http.StatusCode, new { success = false, message = http.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Conflict ? http.Message : fallback });
        return StatusCode(502, new { success = false, message = fallback });
    }
}
