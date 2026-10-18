using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientProblems;
using MicroEMR.Application.PatientProblems.Contracts;
using MicroEMR.Application.PatientProblems.Services;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission(PermissionKeys.PatientsView)]
[Route("api/patients/{patientUid:guid}/problems")]
public sealed class PatientProblemsController(IPatientProblemService service, ILogger<PatientProblemsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PatientProblemResponse>>> GetAll(Guid patientUid, [FromQuery] string status = "Active", CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty) return BadRequest();
        if (!IsValidStatus(status)) return BadRequest(new { message = "Status must be Active, Resolved, or All." });
        return Ok(await service.GetByPatientUidAsync(patientUid, status, cancellationToken));
    }

    [HttpGet("{patientProblemUid:guid}")]
    public async Task<ActionResult<PatientProblemResponse>> Get(Guid patientUid, Guid patientProblemUid, CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty || patientProblemUid == Guid.Empty) return BadRequest();
        var problem = await service.GetByUidAsync(patientUid, patientProblemUid, cancellationToken);
        return problem is null ? NotFound() : Ok(problem);
    }

    [HttpPost, RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<ActionResult<PatientProblemResponse>> Create(Guid patientUid, CreatePatientProblemRequest request, CancellationToken cancellationToken = default)
    {
        request.RequestCorrelationId = HttpContext.TraceIdentifier;
        return Write(patientUid, async () =>
        {
            var created = await service.CreateAsync(patientUid, request, UserId(), cancellationToken);
            return CreatedAtAction(nameof(Get), new { patientUid, patientProblemUid = created.PatientProblemUid }, created);
        }, cancellationToken);
    }

    [HttpPut("{patientProblemUid:guid}"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<ActionResult<PatientProblemResponse>> Update(Guid patientUid, Guid patientProblemUid, UpdatePatientProblemRequest request, CancellationToken cancellationToken = default)
    {
        request.RequestCorrelationId = HttpContext.TraceIdentifier;
        return Write(patientUid, async () =>
        {
            if (patientProblemUid == Guid.Empty) return BadRequest();
            var result = await service.UpdateAsync(patientUid, patientProblemUid, request, UserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }, cancellationToken);
    }

    [HttpPost("{patientProblemUid:guid}/resolve"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public Task<ActionResult<PatientProblemResponse>> Resolve(Guid patientUid, Guid patientProblemUid, ResolvePatientProblemRequest request, CancellationToken cancellationToken = default)
    {
        request.RequestCorrelationId = HttpContext.TraceIdentifier;
        return Write(patientUid, async () =>
        {
            if (patientProblemUid == Guid.Empty) return BadRequest();
            var result = await service.ResolveAsync(patientUid, patientProblemUid, request, UserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }, cancellationToken);
    }

    private async Task<ActionResult<PatientProblemResponse>> Write(Guid patientUid, Func<Task<ActionResult<PatientProblemResponse>>> operation, CancellationToken token)
    {
        if (patientUid == Guid.Empty) return BadRequest();
        try { return await operation(); }
        catch (PatientProblemValidationException error) { return BadRequest(new { message = error.Message }); }
        catch (PatientProblemNotFoundException) { return NotFound(); }
        catch (PatientProblemConcurrencyException) { return Conflict(new { message = "This problem changed. Reload and review before saving again." }); }
        catch (PatientProblemResolvedException) { return Conflict(new { message = "This problem is already resolved. Reload and review its history." }); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            logger.LogError(error, "Patient problem mutation failed for patient {PatientUid}.", patientUid);
            return StatusCode(503, new { message = "The problem could not be saved. Reload before retrying." });
        }
    }

    private static bool IsValidStatus(string? status) => new[] { "Active", "Resolved", "All" }.Contains(status?.Trim(), StringComparer.OrdinalIgnoreCase);
    private long UserId() => ClinicalUserActorContext.GetRequired(HttpContext);
}
