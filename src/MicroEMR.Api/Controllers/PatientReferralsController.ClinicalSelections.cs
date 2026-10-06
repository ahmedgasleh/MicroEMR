using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientReferrals;

namespace MicroEMR.Api.Controllers;

public sealed partial class PatientReferralsController
{
    [HttpGet("clinical-options"),RequirePermission(PermissionKeys.ReferralsManage)]
    public async Task<IActionResult> ClinicalOptions(Guid patientUid,CancellationToken cancellationToken)
    {
        try { return Ok(await service.GetClinicalSelectionOptionsAsync(patientUid,cancellationToken)); }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
        catch (PatientReferralPatientNotFoundException) { return NotFound(); }
    }

    [HttpGet("{referralUid:guid}/clinical-selections")]
    public Task<IActionResult> ClinicalSelections(Guid patientUid,Guid referralUid,CancellationToken cancellationToken) =>
        ClinicalSelectionOperation(()=>service.GetClinicalSelectionsAsync(patientUid,referralUid,cancellationToken));

    [HttpPut("{referralUid:guid}/clinical-selections"),RequirePermission(PermissionKeys.ReferralsManage)]
    public Task<IActionResult> ReplaceClinicalSelections(Guid patientUid,Guid referralUid,
        [FromBody] ReplacePatientReferralClinicalSelectionsRequest request,CancellationToken cancellationToken) =>
        ClinicalSelectionOperation(()=>service.ReplaceDraftClinicalSelectionsAsync(patientUid,referralUid,request,cancellationToken));

    private async Task<IActionResult> ClinicalSelectionOperation(Func<Task<PatientReferralClinicalSelectionsResponse?>> operation)
    {
        try { var response=await operation(); return response is null ? NotFound() : Ok(response); }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
        catch (PatientReferralPatientNotFoundException) { return NotFound(); }
        catch (PatientReferralConcurrencyException e) { return Conflict(new {message=e.Message,code="referral_concurrency_conflict"}); }
        catch (PatientReferralTransitionException e) { return Conflict(new {message=e.Message}); }
        catch (ReferralClinicalSelectionRuleException e) { return BadRequest(new {message=e.Message}); }
        catch (ArgumentException e) { return BadRequest(new {message=e.Message}); }
    }
}
