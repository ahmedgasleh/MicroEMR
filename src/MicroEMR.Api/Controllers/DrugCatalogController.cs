using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.DrugCatalog;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/drug-catalog")]
public sealed class DrugCatalogController(DrugCatalogService catalog) : ControllerBase
{
    [HttpGet, RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
    public async Task<IActionResult> Search(string? query, int page = 1, CancellationToken token = default)
    {
        try { return Ok(await catalog.SearchAsync(query, page, token)); }
        catch (DrugCatalogUnavailableException e) { return StatusCode(503, new { message = e.Message }); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }
    [HttpGet("{uid:guid}"), RequirePermission(PermissionKeys.PrescriptionsPrescribe)]
    public async Task<IActionResult> Get(Guid uid, CancellationToken token)
    {
        try { return await catalog.GetAsync(uid, token) is { } row ? Ok(row) : NotFound(); }
        catch (DrugCatalogUnavailableException e) { return StatusCode(503, new { message = e.Message }); }
    }

    [HttpPost("import"), RequirePermission(PermissionKeys.ClinicSettingsManage), RequestSizeLimit(34 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile archive, CancellationToken token)
    {
        if (archive.Length is 0 or > 32 * 1024 * 1024) return BadRequest(new { message = "Select a DPD ZIP archive up to 32 MB." });
        try
        {
            await using var stream = archive.OpenReadStream();
            return Ok(await catalog.ImportAsync(stream, ClinicalUserActorContext.GetRequired(HttpContext), token));
        }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
        catch (DrugCatalogUnavailableException e) { return StatusCode(503, new { message = e.Message }); }
        catch (IOException) { return StatusCode(503, new { message = "The source archive could not be read. No import was completed." }); }
    }
}
