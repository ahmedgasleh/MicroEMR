using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientReferrals;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/patients/{patientUid:guid}/referrals/{referralUid:guid}/documents")]
[RequirePermission(PermissionKeys.ReferralsView)]
public sealed class PatientReferralDocumentsController(IReferralDocumentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReferralDocumentLinkResponse>>> Get(
        Guid patientUid, Guid referralUid, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(patientUid, referralUid, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{documentUid:guid}"), RequirePermission(PermissionKeys.ReferralsManage)]
    public Task<IActionResult> Link(Guid patientUid, Guid referralUid, Guid documentUid,
        [FromBody] ReferralDocumentMutationRequest request, CancellationToken cancellationToken) =>
        Mutate(() => service.LinkAsync(patientUid, referralUid, documentUid, request, cancellationToken));

    [HttpDelete("{documentUid:guid}"), RequirePermission(PermissionKeys.ReferralsManage)]
    public Task<IActionResult> Unlink(Guid patientUid, Guid referralUid, Guid documentUid,
        [FromBody] ReferralDocumentMutationRequest request, CancellationToken cancellationToken) =>
        Mutate(() => service.UnlinkAsync(patientUid, referralUid, documentUid, request, cancellationToken));

    private async Task<IActionResult> Mutate(Func<Task> action)
    {
        try { await action(); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ReferralDocumentConcurrencyException e) { return Conflict(new { message = e.Message, code = "referral_concurrency_conflict" }); }
        catch (ReferralDocumentRuleException e) { return Conflict(new { message = e.Message, code = "referral_document_rule" }); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }
}
