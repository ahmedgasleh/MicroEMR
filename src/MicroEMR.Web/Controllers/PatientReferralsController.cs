using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.PatientReferrals;
using MicroEMR.Web.Services.PatientReferrals;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class PatientReferralsController(
    IPatientReferralApiClient client,
    ILogger<PatientReferralsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> List(
        Guid patientUid,
        CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty)
            return BadRequest(new { success = false, message = "A patient is required." });

        try
        {
            var referrals = await client.GetByPatientUidAsync(patientUid, cancellationToken);
            return Json(new { success = true, referrals });
        }
        catch (HttpRequestException exception)
        {
            return ApiFailure(exception, "Referral list could not be loaded.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to load referrals for patient {PatientUid}.", patientUid);
            return StatusCode(502, new { success = false, message = "Referral list could not be loaded." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid patientUid,
        Guid referralUid,
        CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty || referralUid == Guid.Empty)
            return BadRequest(new { success = false, message = "The referral request is invalid." });

        try
        {
            var referral = await client.GetByUidAsync(patientUid, referralUid, cancellationToken);
            return referral is null
                ? NotFound(new { success = false, message = "Referral was not found." })
                : Json(new { success = true, referral });
        }
        catch (HttpRequestException exception)
        {
            return ApiFailure(exception, "Referral details could not be loaded.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to load referral {ReferralUid} for patient {PatientUid}.",
                referralUid,
                patientUid);
            return StatusCode(502, new { success = false, message = "Referral details could not be loaded." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreatePatientReferralViewModel model,
        CancellationToken cancellationToken = default)
    {
        model.RecipientName = model.RecipientName?.Trim() ?? string.Empty;
        model.Reason = model.Reason?.Trim() ?? string.Empty;
        if (model.PatientUid == Guid.Empty)
            ModelState.AddModelError(nameof(model.PatientUid), "A patient is required.");
        if (string.IsNullOrWhiteSpace(model.RecipientName))
            ModelState.AddModelError(nameof(model.RecipientName), "Recipient name is required.");
        if (string.IsNullOrWhiteSpace(model.Reason))
            ModelState.AddModelError(nameof(model.Reason), "Referral reason is required.");
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Please correct the referral information." });

        try
        {
            var referral = await client.CreateAsync(model.PatientUid, model, cancellationToken);
            return referral is null
                ? NotFound(new { success = false, message = "Patient was not found." })
                : Json(new { success = true, referral });
        }
        catch (HttpRequestException exception)
        {
            return ApiFailure(exception, "The referral could not be created.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create referral for patient {PatientUid}.", model.PatientUid);
            return StatusCode(502, new { success = false, message = "The referral could not be created." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MarkSent(
        ReferralStatusTransitionViewModel model, CancellationToken cancellationToken = default) =>
        TransitionAsync(model, client.MarkSentAsync, "Referral marked as Sent.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MarkResponseReceived(
        ReferralStatusTransitionViewModel model, CancellationToken cancellationToken = default) =>
        TransitionAsync(model, client.MarkResponseReceivedAsync,
            "Referral marked as Response Received.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Close(
        ReferralStatusTransitionViewModel model, CancellationToken cancellationToken = default) =>
        TransitionAsync(model, client.CloseAsync, "Referral closed.", cancellationToken);

    private async Task<IActionResult> TransitionAsync(
        ReferralStatusTransitionViewModel model,
        Func<Guid, Guid, string, CancellationToken, Task<PatientReferralDetailsViewModel?>> transition,
        string successMessage,
        CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty || model.ReferralUid == Guid.Empty ||
            string.IsNullOrWhiteSpace(model.RowVersion))
            return BadRequest(new { success = false, message = "The referral request is invalid." });
        try
        {
            var referral = await transition(
                model.PatientUid, model.ReferralUid, model.RowVersion, cancellationToken);
            return referral is null
                ? NotFound(new { success = false, message = "Referral was not found." })
                : Json(new { success = true, referral, message = successMessage });
        }
        catch (HttpRequestException exception)
        {
            return ApiFailure(exception, "The referral status could not be changed.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Failed to transition referral {ReferralUid} for patient {PatientUid}.",
                model.ReferralUid, model.PatientUid);
            return StatusCode(502, new { success = false, message = "The referral status could not be changed." });
        }
    }

    private IActionResult ApiFailure(HttpRequestException exception, string fallbackMessage)
    {
        logger.LogWarning(exception, "Referral API request failed with status {StatusCode}.", exception.StatusCode);
        var message = string.IsNullOrWhiteSpace(exception.Message) ? fallbackMessage : exception.Message;
        return exception.StatusCode switch
        {
            HttpStatusCode.BadRequest => BadRequest(new { success = false, message }),
            HttpStatusCode.Unauthorized => Unauthorized(new { success = false, message }),
            HttpStatusCode.Forbidden => StatusCode(403, new { success = false, message }),
            HttpStatusCode.NotFound => NotFound(new { success = false, message }),
            HttpStatusCode.Conflict => Conflict(new { success = false, message }),
            _ => StatusCode(502, new { success = false, message = fallbackMessage })
        };
    }
}
