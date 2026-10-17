using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientRiskFactors;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.PatientRiskFactors;

namespace MicroEMR.Web.Controllers;

[Authorize, RequireWebPermission(PermissionKeys.PatientsView), Route("patients/{patientUid:guid}/risk-factors")]
public sealed class PatientRiskFactorsController(IPatientRiskFactorApiClient client) : Controller
{
    [HttpGet] public Task<IActionResult> List(Guid patientUid, string status = "Active", CancellationToken token = default) =>
        Run(async () => Json(new { success = true, items = await client.List(patientUid, status, token) }));
    [HttpGet("{riskFactorUid:guid}")] public Task<IActionResult> Get(Guid patientUid, Guid riskFactorUid, CancellationToken token) =>
        Run(async () => await client.Get(patientUid, riskFactorUid, token) is { } row ? Json(new { success = true, item = row }) : NotFound());
    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Create(Guid patientUid, CreatePatientRiskFactorRequest request, CancellationToken token) => Run(async () =>
    {
        if (!ModelState.IsValid) return Invalid();
        var payload = new UpdatePatientRiskFactorRequest { RiskFactor = request.RiskFactor, ExposureDetails = request.ExposureDetails, LifeStageCode = request.LifeStageCode, Notes = request.Notes };
        return Json(new { success = true, item = await client.Create(patientUid, payload, token) });
    });
    [HttpPost("{riskFactorUid:guid}"), ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Update(Guid patientUid, Guid riskFactorUid, UpdatePatientRiskFactorRequest request, CancellationToken token) => Run(async () =>
    {
        if (!ModelState.IsValid) return Invalid();
        return await client.Update(patientUid, riskFactorUid, request, token) is { } row ? Json(new { success = true, item = row }) : NotFound();
    });
    [HttpPost("{riskFactorUid:guid}/archive"), ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Archive(Guid patientUid, Guid riskFactorUid, ArchivePatientRiskFactorRequest request, CancellationToken token) => Run(async () =>
    {
        if (!ModelState.IsValid) return Invalid();
        return await client.Archive(patientUid, riskFactorUid, request.RowVersion, token) is { } row ? Json(new { success = true, item = row }) : NotFound();
    });
    private BadRequestObjectResult Invalid() => BadRequest(new { success = false, message = "Check the risk factor, exposure details and notes." });
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (HttpRequestException e) { return StatusCode(e.StatusCode is { } status ? (int)status : 502, new { success = false, message = e.Message }); }
    }
}
