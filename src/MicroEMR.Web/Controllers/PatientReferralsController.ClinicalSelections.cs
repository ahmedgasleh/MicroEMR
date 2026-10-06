using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientReferrals;

namespace MicroEMR.Web.Controllers;

public sealed class ReferralClinicalSelectionSaveModel
{
    public Guid PatientUid { get; set; }
    public Guid ReferralUid { get; set; }
    public required string RowVersion { get; set; }
    public IReadOnlyList<ReferralClinicalSelectionInput> Selections { get; set; } = [];
}

public sealed partial class PatientReferralsController
{
    [HttpGet]
    public async Task<IActionResult> ClinicalDocumentOptions(Guid patientUid,Guid? referralUid,CancellationToken token)
    {
        try
        {
            var documents=await (documentClient ?? throw new InvalidOperationException("Document client unavailable."))
                .GetByPatientUidAsync(patientUid,token);
            var linked=referralUid.HasValue?await client.GetLinkedDocumentsAsync(patientUid,referralUid.Value,token):[];
            return Json(new {success=true,linked,available=documents.Select(x=>new {x.DocumentUid,x.Title,x.DocumentType,DocumentStatus=x.Status,CreatedAtUtc=x.CreatedAt})});
        }
        catch(HttpRequestException e) {return ApiFailure(e,"Supporting documents could not be loaded.");}
    }
    [HttpGet]
    public async Task<IActionResult> ClinicalOptions(Guid patientUid,CancellationToken token)
    {
        try { return Json(new {success=true,options=await client.GetClinicalOptionsAsync(patientUid,token)}); }
        catch(HttpRequestException e) {return ApiFailure(e,"Clinical choices could not be loaded.");}
    }
    [HttpGet]
    public async Task<IActionResult> ClinicalSelections(Guid patientUid,Guid referralUid,CancellationToken token)
    {
        try {var selections=await client.GetClinicalSelectionsAsync(patientUid,referralUid,token);
            return selections is null?NotFound():Json(new {success=true,selections});}
        catch(HttpRequestException e) {return ApiFailure(e,"Clinical selections could not be loaded.");}
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplaceClinicalSelections([FromBody] ReferralClinicalSelectionSaveModel model,CancellationToken token)
    {
        if(!ModelState.IsValid || model.PatientUid==Guid.Empty || model.ReferralUid==Guid.Empty)return BadRequest();
        try {var selections=await client.ReplaceClinicalSelectionsAsync(model.PatientUid,model.ReferralUid,
            new() {RowVersion=model.RowVersion,Selections=model.Selections},token);
            return selections is null?NotFound():Json(new {success=true,selections});}
        catch(HttpRequestException e) {return ApiFailure(e,"Clinical selections could not be saved.");}
    }
}
