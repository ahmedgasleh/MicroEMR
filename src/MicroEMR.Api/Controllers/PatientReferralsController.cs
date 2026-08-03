using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.PatientReferrals;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
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
}
