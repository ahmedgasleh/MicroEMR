using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Cds;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, RequirePermission(PermissionKeys.PatientsView)]
[Route("api/patients/{patientUid:guid}/cds")]
public sealed class PatientCdsController(ICdsEvaluationService evaluationService, ICdsRepository repository) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CdsAlertResponse>> List(Guid patientUid, bool includeHistory = false,
        CancellationToken cancellationToken = default) =>
        repository.ListAsync(patientUid, includeHistory, cancellationToken);

    [HttpGet("{alertUid:guid}/history")]
    public Task<IReadOnlyList<CdsAlertHistoryResponse>> History(Guid patientUid, Guid alertUid,
        CancellationToken cancellationToken = default) =>
        repository.GetHistoryAsync(patientUid, alertUid, cancellationToken);

    [HttpPost("evaluate")]
    public async Task<ActionResult<CdsEvaluationResponse>> Evaluate(Guid patientUid,
        CancellationToken cancellationToken) =>
        await evaluationService.EvaluatePatientAsync(patientUid, cancellationToken);

    [HttpPost("{alertUid:guid}/acknowledge"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public async Task<IActionResult> Acknowledge(Guid patientUid, Guid alertUid,
        AcknowledgeCdsAlertRequest request, CancellationToken cancellationToken)
    {
        if (!TryRowVersion(request.ExpectedRowVersion, out var rowVersion))
            return BadRequest(new { message = "The CDS alert row version is invalid." });
        try
        {
            return await repository.AcknowledgeAsync(patientUid, alertUid, rowVersion,
                ClinicalUserActorContext.GetRequired(HttpContext), cancellationToken) is { } alert
                ? Ok(alert) : NotFound();
        }
        catch (CdsConcurrencyException) { return Conflict(new { message = "The CDS alert changed. Reload and try again." }); }
        catch (CdsInvalidTransitionException) { return Conflict(new { message = "The CDS alert cannot be acknowledged from its current state." }); }
    }

    [HttpPost("{alertUid:guid}/dismiss"), RequirePermission(PermissionKeys.ClinicalDataManage)]
    public async Task<IActionResult> Dismiss(Guid patientUid, Guid alertUid,
        DismissCdsAlertRequest request, CancellationToken cancellationToken)
    {
        if (!CdsDismissReasons.All.Contains(request.ReasonCode) ||
            request.ReasonCode == CdsDismissReasons.Other && string.IsNullOrWhiteSpace(request.Comment))
            return BadRequest(new { message = "A governed dismissal reason is required; Other requires a comment." });
        if (!TryRowVersion(request.ExpectedRowVersion, out var rowVersion))
            return BadRequest(new { message = "The CDS alert row version is invalid." });
        try
        {
            return await repository.DismissAsync(patientUid, alertUid, request.ReasonCode,
                request.Comment, rowVersion, ClinicalUserActorContext.GetRequired(HttpContext), cancellationToken) is { } alert
                ? Ok(alert) : NotFound();
        }
        catch (CdsConcurrencyException) { return Conflict(new { message = "The CDS alert changed. Reload and try again." }); }
        catch (CdsInvalidTransitionException) { return Conflict(new { message = "The CDS alert cannot be dismissed from its current state." }); }
        catch (CdsInvalidDismissReasonException) { return BadRequest(new { message = "The dismissal reason is invalid." }); }
    }

    private static bool TryRowVersion(string value, out byte[] rowVersion)
    {
        try { rowVersion = Convert.FromBase64String(value); return rowVersion.Length == 8; }
        catch (FormatException) { rowVersion = []; return false; }
    }
}
