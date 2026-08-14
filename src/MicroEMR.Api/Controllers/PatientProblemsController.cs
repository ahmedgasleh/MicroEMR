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
    public async Task<ActionResult<PatientProblemResponse>> Create(Guid patientUid, CreatePatientProblemRequest request, CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty) return BadRequest();
        request.ProblemName = request.ProblemName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(request.ProblemName)) ModelState.AddModelError(nameof(request.ProblemName), "Problem name is required.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var created = await service.CreateAsync(patientUid, request, UserId(), cancellationToken);
            return CreatedAtAction(nameof(Get), new { patientUid, patientProblemUid = created.PatientProblemUid }, created);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Unable to create a problem for patient {PatientUid}.", patientUid);
            return BadRequest(new { message = "The problem could not be created." });
        }
    }

    [HttpPut("{patientProblemUid:guid}"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public async Task<ActionResult<PatientProblemResponse>> Update(Guid patientUid, Guid patientProblemUid, UpdatePatientProblemRequest request, CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty || patientProblemUid == Guid.Empty) return BadRequest();
        request.ProblemName = request.ProblemName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(request.ProblemName)) ModelState.AddModelError(nameof(request.ProblemName), "Problem name is required.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var result = await service.UpdateAsync(patientUid, patientProblemUid, request, UserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (PatientProblemResolvedException)
        {
            return Conflict(new { message = "Resolved problems cannot be edited." });
        }
    }

    [HttpPost("{patientProblemUid:guid}/resolve"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public async Task<ActionResult<PatientProblemResponse>> Resolve(Guid patientUid, Guid patientProblemUid, ResolvePatientProblemRequest request, CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty || patientProblemUid == Guid.Empty) return BadRequest();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var result = await service.ResolveAsync(patientUid, patientProblemUid, request, UserId(), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    private static bool IsValidStatus(string? status) => new[] { "Active", "Resolved", "All" }.Contains(status?.Trim(), StringComparer.OrdinalIgnoreCase);
    private long UserId() => ClinicalUserActorContext.GetRequired(HttpContext);
}
