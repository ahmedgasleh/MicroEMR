using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.PatientReferrals;
using MicroEMR.Web.Services.PatientDocuments;
using MicroEMR.Web.Services.PatientReferrals;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class ReferralSupportingDocumentsController(
    IPatientReferralApiClient referrals,
    IPatientDocumentApiClient documents,
    ILogger<ReferralSupportingDocumentsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> List(Guid patientUid, Guid referralUid,
        CancellationToken cancellationToken)
    {
        try
        {
            var linked = await referrals.GetLinkedDocumentsAsync(patientUid, referralUid, cancellationToken);
            var availableDocuments = await documents.GetByPatientUidAsync(patientUid, cancellationToken);
            var available = availableDocuments.Select(document => new
            {
                document.DocumentUid,
                document.Title,
                document.DocumentType,
                DocumentStatus = document.Status,
                CreatedAtUtc = document.CreatedAt
            });
            return Json(new { success = true, linked, available });
        }
        catch (Exception e) when (e is HttpRequestException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Unable to load supporting documents for referral {ReferralUid}.", referralUid);
            return StatusCode(502, new { success = false, message = "Supporting documents could not be loaded." });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Link(ReferralDocumentMutationViewModel model, CancellationToken cancellationToken) =>
        Mutate(model, referrals.LinkDocumentAsync, cancellationToken);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Unlink(ReferralDocumentMutationViewModel model, CancellationToken cancellationToken) =>
        Mutate(model, referrals.UnlinkDocumentAsync, cancellationToken);

    private async Task<IActionResult> Mutate(ReferralDocumentMutationViewModel model,
        Func<Guid, Guid, Guid, string, CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty || model.ReferralUid == Guid.Empty ||
            model.DocumentUid == Guid.Empty || string.IsNullOrWhiteSpace(model.RowVersion))
            return BadRequest(new { success = false, message = "The supporting-document request is invalid." });
        try
        {
            await action(model.PatientUid, model.ReferralUid, model.DocumentUid, model.RowVersion, cancellationToken);
            return Json(new { success = true });
        }
        catch (HttpRequestException e)
        {
            logger.LogWarning(e, "Supporting-document mutation was rejected for referral {ReferralUid}.", model.ReferralUid);
            var status = e.StatusCode is System.Net.HttpStatusCode.Conflict ? 409 : 502;
            return StatusCode(status, new { success = false, message = e.StatusCode is System.Net.HttpStatusCode.Conflict
                ? "The referral changed or the document link is no longer valid. Refresh and try again."
                : "The supporting document could not be changed." });
        }
    }
}
