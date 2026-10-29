using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.PatientPrescriptions;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class DrugCatalogController(DrugCatalogApiClient client) : Controller
{
    [HttpGet, RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)]
    public async Task<IActionResult> Lookup(string? query, int page = 1, CancellationToken token = default)
    {
        try { return Json(await client.Search(query, page, token)); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "Drug catalogue unavailable. Manual entry remains available." }); }
    }
    [HttpGet, RequireWebPermission(PermissionKeys.PrescriptionsPrescribe)]
    public async Task<IActionResult> Detail(Guid uid, CancellationToken token)
    {
        try { return Json(await client.Get(uid, token)); }
        catch (HttpRequestException) { return StatusCode(503, new { message = "Drug catalogue product unavailable. Search again." }); }
    }
    [HttpGet, RequireWebPermission(PermissionKeys.ClinicSettingsManage)]
    public IActionResult Import() => View();
    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.ClinicSettingsManage), RequestSizeLimit(34 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile? archive, CancellationToken token)
    {
        if (archive is null || archive.Length is 0 or > 32 * 1024 * 1024) ModelState.AddModelError("archive", "Select a ZIP archive up to 32 MB.");
        if (ModelState.IsValid) try
        {
            await using var stream = archive!.OpenReadStream();
            var result = await client.Import(stream, token);
            ViewData["Result"] = $"Imported: {result!.Inserted} new, {result.Updated} changed, {result.Unchanged} unchanged.";
        }
        catch (HttpRequestException e) { ModelState.AddModelError("", e.Message); }
        return View();
    }
}
