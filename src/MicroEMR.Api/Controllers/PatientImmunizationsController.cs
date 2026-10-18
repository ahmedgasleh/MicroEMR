using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientImmunizations;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/patients/{patientUid:guid}/immunizations")]
[RequirePermission(PermissionKeys.PatientsView)]
public sealed class PatientImmunizationsController(IPatientImmunizationService service, ILogger<PatientImmunizationsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PatientImmunizationResponse>>> List(Guid patientUid, string status = "All", CancellationToken token = default) =>
        Ok(await service.ListAsync(patientUid, status, token));

    [HttpGet("{immunizationUid:guid}")]
    public async Task<ActionResult<PatientImmunizationResponse>> Get(Guid patientUid, Guid immunizationUid, CancellationToken token) =>
        await service.GetAsync(patientUid, immunizationUid, token) is { } item ? Ok(item) : NotFound();

    [HttpGet("summary/pdf")]
    public async Task<IActionResult> Summary(Guid patientUid,
        [FromServices] IPatientImmunizationSummaryService summary, CancellationToken token)
    {
        var bytes = await summary.GenerateAsync(patientUid, HttpContext.TraceIdentifier, token);
        Response.Headers.CacheControl = "no-store";
        return bytes is null ? NotFound() : File(bytes, "application/pdf");
    }

    [HttpPost, RequirePermission(PermissionKeys.ClinicalDataManage)]
    public async Task<ActionResult<PatientImmunizationResponse>> Create(Guid patientUid, CreatePatientImmunizationRequest request, CancellationToken token)
    {
        request.RequestCorrelationId = HttpContext.TraceIdentifier;
        return await Write(async () =>
        {
            var item = await service.CreateAsync(patientUid, request, Actor(), token);
            return CreatedAtAction(nameof(Get), new { patientUid, immunizationUid = item.ImmunizationUid }, item);
        }, token);
    }

    [HttpPut("{immunizationUid:guid}"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public async Task<ActionResult<PatientImmunizationResponse>> Update(Guid patientUid, Guid immunizationUid, UpdatePatientImmunizationRequest request, CancellationToken token)
    {
        request.RequestCorrelationId = HttpContext.TraceIdentifier;
        return await Write(async () => await service.UpdateAsync(patientUid, immunizationUid, request, Actor(), token) is { } item ? Ok(item) : NotFound(), token);
    }

    [HttpPost("{immunizationUid:guid}/entered-in-error"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public async Task<ActionResult<PatientImmunizationResponse>> MarkEnteredInError(Guid patientUid, Guid immunizationUid, MarkImmunizationEnteredInErrorRequest request, CancellationToken token)
    {
        request.RequestCorrelationId = HttpContext.TraceIdentifier;
        return await Write(async () => await service.MarkEnteredInErrorAsync(patientUid, immunizationUid, request, Actor(), token) is { } item ? Ok(item) : NotFound(), token);
    }

    private async Task<ActionResult<PatientImmunizationResponse>> Write(Func<Task<ActionResult<PatientImmunizationResponse>>> operation, CancellationToken token)
    {
        try { return await operation(); }
        catch (PatientImmunizationValidationException error) { return BadRequest(new { message = error.Message }); }
        catch (PatientImmunizationNotFoundException) { return NotFound(); }
        catch (PatientImmunizationConcurrencyException) { return Conflict(new { message = "This immunization changed. Reload and review before saving again." }); }
        catch (PatientImmunizationTerminalException) { return Conflict(new { message = "Entered-in-error immunizations cannot be changed. Reload and review the retained history." }); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to save patient immunization.");
            return StatusCode(503, new { message = "The immunization could not be saved. Reload before retrying." });
        }
    }

    private long Actor() => ClinicalUserActorContext.GetRequired(HttpContext);
}
