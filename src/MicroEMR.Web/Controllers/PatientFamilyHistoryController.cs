using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientFamilyHistory;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.PatientFamilyHistory;

namespace MicroEMR.Web.Controllers;

[Authorize, RequireWebPermission(PermissionKeys.PatientsView), Route("patients/{patientUid:guid}/family-history")]
public sealed class PatientFamilyHistoryController(IPatientFamilyHistoryApiClient client) : Controller
{
    [HttpGet] public Task<IActionResult> List(Guid patientUid, string status = "Active", CancellationToken token = default) =>
        Run(async () => Json(new { success = true, items = await client.List(patientUid, status, token) }));
    [HttpGet("{familyHistoryUid:guid}")] public Task<IActionResult> Get(Guid patientUid, Guid familyHistoryUid, CancellationToken token) =>
        Run(async () => await client.Get(patientUid, familyHistoryUid, token) is { } row ? Json(new { success = true, item = row }) : NotFound());
    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Create(Guid patientUid, CreatePatientFamilyHistoryRequest request, CancellationToken token) => Run(async () =>
    {
        if (!ModelState.IsValid) return Invalid();
        var payload = new UpdatePatientFamilyHistoryRequest { Relationship = request.Relationship, ConditionDescription = request.ConditionDescription,
            AgeAtOnset = request.AgeAtOnset, LifeStageCode = request.LifeStageCode, Treatment = request.Treatment, Notes = request.Notes };
        return Json(new { success = true, item = await client.Create(patientUid, payload, token) });
    });
    [HttpPost("{familyHistoryUid:guid}"), ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Update(Guid patientUid, Guid familyHistoryUid, UpdatePatientFamilyHistoryRequest request, CancellationToken token) => Run(async () =>
    {
        if (!ModelState.IsValid) return Invalid();
        return await client.Update(patientUid, familyHistoryUid, request, token) is { } row ? Json(new { success = true, item = row }) : NotFound();
    });
    [HttpPost("{familyHistoryUid:guid}/archive"), ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public Task<IActionResult> Archive(Guid patientUid, Guid familyHistoryUid, ArchivePatientFamilyHistoryRequest request, CancellationToken token) => Run(async () =>
    {
        if (!ModelState.IsValid) return Invalid();
        return await client.Archive(patientUid, familyHistoryUid, request.RowVersion, token) is { } row ? Json(new { success = true, item = row }) : NotFound();
    });
    private BadRequestObjectResult Invalid() => BadRequest(new { success = false, message = "Check the relationship, condition and family history fields." });
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (HttpRequestException e) { return StatusCode(e.StatusCode is { } status ? (int)status : 502, new { success = false, message = e.Message }); }
    }
}
