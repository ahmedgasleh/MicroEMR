using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission(PermissionKeys.ReferralsView)]
[Route("api/patients/{patientUid:guid}/referrals")]
public sealed class PatientReferralsController(
    IPatientReferralService service,
    ILogger<PatientReferralsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PatientReferralListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PatientReferralListItemResponse>>> GetAll(
        Guid patientUid,
        CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty) return BadRequest();

        try
        {
            return Ok(await service.GetByPatientUidAsync(patientUid, cancellationToken));
        }
        catch (PatientReferralPatientNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{referralUid:guid}")]
    [ProducesResponseType<PatientReferralDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientReferralDetailsResponse>> Get(
        Guid patientUid,
        Guid referralUid,
        CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty || referralUid == Guid.Empty) return BadRequest();

        try
        {
            var referral = await service.GetByUidAsync(patientUid, referralUid, cancellationToken);
            return referral is null ? NotFound() : Ok(referral);
        }
        catch (PatientReferralPatientNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.ReferralsManage)]
    [ProducesResponseType<PatientReferralDetailsResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientReferralDetailsResponse>> Create(
        Guid patientUid,
        [FromBody] CreatePatientReferralRequest request,
        CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty) return BadRequest();
        if (string.IsNullOrWhiteSpace(request.RecipientName))
            ModelState.AddModelError(nameof(request.RecipientName), "Recipient name is required.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            ModelState.AddModelError(nameof(request.Reason), "Referral reason is required.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var referral = await service.CreateAsync(patientUid, request, cancellationToken);
            return CreatedAtAction(
                nameof(Get),
                new { patientUid, referralUid = referral.ReferralUid },
                referral);
        }
        catch (PatientReferralPatientNotFoundException)
        {
            return NotFound();
        }
        catch (SqlException exception) when (exception.Number == 51500)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            logger.LogWarning(
                exception,
                "Referral validation failed for patient {PatientUid}.",
                patientUid);
            ModelState.AddModelError(string.Empty, exception.Message);
            return ValidationProblem(ModelState);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to create referral for patient {PatientUid}.",
                patientUid);
            return Problem("The referral could not be created.");
        }
    }

    [HttpGet("providers")]
    public async Task<ActionResult<IReadOnlyList<ReferralProviderListItem>>> Providers(CancellationToken cancellationToken) =>
        Ok(await service.GetActiveProvidersAsync(cancellationToken));

    [HttpPut("{referralUid:guid}"), RequirePermission(PermissionKeys.ReferralsManage)]
    public async Task<ActionResult<PatientReferralDetailsResponse>> UpdateDraft(Guid patientUid, Guid referralUid,
        [FromBody] UpdatePatientReferralDraftRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try { var result=await service.UpdateDraftAsync(patientUid,referralUid,request,cancellationToken);return result is null?NotFound():Ok(result); }
        catch(PatientReferralConcurrencyException e){return Conflict(new{message=e.Message,code="referral_concurrency_conflict"});}
        catch(ArgumentException e){return BadRequest(new{message=e.Message});}
    }

    [HttpGet("{referralUid:guid}/letter/preview"), RequirePermission(PermissionKeys.ReferralsManage)]
    public async Task<IActionResult> PreviewLetter(Guid patientUid,Guid referralUid,CancellationToken cancellationToken)
    {
        try { var bytes=await service.PreviewLetterAsync(patientUid,referralUid,cancellationToken);return bytes is null?NotFound():File(bytes,"application/pdf"); }
        catch(PatientReferralTransitionException e){return Conflict(new{message=e.Message});}
    }

    [HttpGet("{referralUid:guid}/letter")]
    public async Task<IActionResult> Letter(Guid patientUid,Guid referralUid,CancellationToken cancellationToken)
    {
        var artifact=await service.OpenArtifactAsync(patientUid,referralUid,cancellationToken);
        return artifact is null?NotFound():File(artifact.Content,artifact.MimeType,artifact.FileName);
    }

    [HttpPost("{referralUid:guid}/send")]
    [RequirePermission(PermissionKeys.ReferralsManage)]
    public Task<ActionResult<PatientReferralDetailsResponse>> MarkSent(
        Guid patientUid, Guid referralUid, [FromBody] ReferralStatusTransitionRequest request,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(patientUid, referralUid, request, service.MarkSentAsync, cancellationToken);

    [HttpPost("{referralUid:guid}/response-received")]
    [RequirePermission(PermissionKeys.ReferralsManage)]
    public Task<ActionResult<PatientReferralDetailsResponse>> MarkResponseReceived(
        Guid patientUid, Guid referralUid, [FromBody] ReferralStatusTransitionRequest request,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(patientUid, referralUid, request, service.MarkResponseReceivedAsync, cancellationToken);

    [HttpPut("{referralUid:guid}/follow-up")]
    [RequirePermission(PermissionKeys.ReferralsManage)]
    public async Task<ActionResult<PatientReferralDetailsResponse>> SetFollowUp(
        Guid patientUid, Guid referralUid, [FromBody] SetReferralFollowUpRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await service.SetFollowUpDueAsync(patientUid, referralUid, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (PatientReferralConcurrencyException e) { return Conflict(new { message=e.Message,code="referral_concurrency_conflict" }); }
        catch (PatientReferralTransitionException e) { return Conflict(new { message=e.Message,code="referral_followup_rule" }); }
        catch (ArgumentException e) { return BadRequest(new { message=e.Message }); }
    }

    [HttpPut("{referralUid:guid}/response-document")]
    [RequirePermission(PermissionKeys.ReferralsManage)]
    public async Task<ActionResult<PatientReferralDetailsResponse>> SetResponseDocument(
        Guid patientUid, Guid referralUid, [FromBody] ReferralResponseDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await service.SetResponseDocumentAsync(patientUid, referralUid, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (PatientReferralConcurrencyException e) { return Conflict(new { message=e.Message,code="referral_concurrency_conflict" }); }
        catch (PatientReferralTransitionException e) { return Conflict(new { message=e.Message,code="referral_response_document_rule" }); }
        catch (ArgumentException e) { return BadRequest(new { message=e.Message }); }
    }

    [HttpDelete("{referralUid:guid}/response-document")]
    [RequirePermission(PermissionKeys.ReferralsManage)]
    public async Task<ActionResult<PatientReferralDetailsResponse>> ClearResponseDocument(
        Guid patientUid, Guid referralUid, [FromBody] ReferralStatusTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await service.ClearResponseDocumentAsync(patientUid, referralUid, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (PatientReferralConcurrencyException e) { return Conflict(new { message=e.Message,code="referral_concurrency_conflict" }); }
        catch (PatientReferralTransitionException e) { return Conflict(new { message=e.Message,code="referral_response_document_rule" }); }
        catch (ArgumentException e) { return BadRequest(new { message=e.Message }); }
    }

    [HttpPost("{referralUid:guid}/close")]
    [RequirePermission(PermissionKeys.ReferralsManage)]
    public Task<ActionResult<PatientReferralDetailsResponse>> Close(
        Guid patientUid, Guid referralUid, [FromBody] ReferralStatusTransitionRequest request,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(patientUid, referralUid, request, service.CloseAsync, cancellationToken);

    private async Task<ActionResult<PatientReferralDetailsResponse>> TransitionAsync(
        Guid patientUid,
        Guid referralUid,
        ReferralStatusTransitionRequest request,
        Func<Guid, Guid, ReferralStatusTransitionRequest, CancellationToken,
            Task<PatientReferralDetailsResponse?>> transition,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || referralUid == Guid.Empty) return BadRequest();
        if (string.IsNullOrWhiteSpace(request.RowVersion))
        {
            ModelState.AddModelError(nameof(request.RowVersion), "RowVersion is required.");
            return ValidationProblem(ModelState);
        }

        try
        {
            var referral = await transition(patientUid, referralUid, request, cancellationToken);
            return referral is null ? NotFound() : Ok(referral);
        }
        catch (PatientReferralPatientNotFoundException)
        {
            return NotFound();
        }
        catch (PatientReferralConcurrencyException exception)
        {
            return Conflict(new { message = exception.Message, code = "referral_concurrency_conflict" });
        }
        catch (PatientReferralTransitionException exception)
        {
            return Conflict(new { message = exception.Message, code = "invalid_referral_transition" });
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return ValidationProblem(ModelState);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Failed to transition referral {ReferralUid} for patient {PatientUid}.",
                referralUid, patientUid);
            return Problem("The referral status could not be changed.");
        }
    }
}
