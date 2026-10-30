using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Scheduling;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.Scheduling;

namespace MicroEMR.Web.Controllers;

[Authorize, RequireWebPermission(PermissionKeys.SchedulingManage), RequireWebPermission(PermissionKeys.ProvidersView)]
public sealed class SchedulingResourcesController(SchedulingResourceAdministrationApiClient client,
    ILogger<SchedulingResourcesController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string status = "All", CancellationToken token = default)
    {
        if (status is not ("Active" or "Inactive" or "All")) status = "All";
        try { return View(new SchedulingResourceIndexModel(status, await client.ListAsync(status, token))); }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Unable to load scheduling resources.");
            Response.StatusCode = (int?)exception.StatusCode ?? 503;
            ViewData["LoadError"] = "Scheduling resources could not be loaded. Verify your access and that the resource administration migration is installed.";
            return View(new SchedulingResourceIndexModel(status, []));
        }
    }
    [HttpGet]
    public async Task<IActionResult> Add(CancellationToken token) => await Editor(null, new(), token);
    [HttpGet]
    public async Task<IActionResult> Edit(Guid uid, CancellationToken token)
    {
        try
        {
            var item = await client.GetAsync(uid, token);
            return item is null ? NotFound() : await Editor(uid, new()
            {
                ResourceType = item.ResourceType, DisplayName = item.DisplayName, ColorCode = item.ColorCode,
                SortOrder = item.SortOrder, ProviderUid = item.ProviderUid, RowVersion = item.RowVersion
            }, token);
        }
        catch (HttpRequestException exception) { return LoadFailure(exception); }
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Guid? uid, SaveSchedulingResourceRequest request, CancellationToken token)
    {
        if (!ModelState.IsValid) return await Editor(uid, request, token);
        try
        {
            if (await client.SaveAsync(uid, request, token) is null) return NotFound();
            TempData["SuccessMessage"] = "Scheduling resource saved. Refresh Scheduling and select the resource to display it.";
            return RedirectToAction(nameof(Index));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Scheduling resource save rejected.");
            ModelState.AddModelError("", exception.Message);
            return await Editor(uid, request, token);
        }
    }
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Activate(Guid uid, string rowVersion, CancellationToken token) => SetActive(uid, true, rowVersion, token);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Deactivate(Guid uid, string rowVersion, CancellationToken token) => SetActive(uid, false, rowVersion, token);
    private async Task<IActionResult> SetActive(Guid uid, bool active, string rowVersion, CancellationToken token)
    {
        try
        {
            if (await client.SetActiveAsync(uid, active, rowVersion, token) is null) return NotFound();
            TempData["SuccessMessage"] = active ? "Scheduling resource activated." : "Scheduling resource deactivated. Historical records are preserved.";
        }
        catch (HttpRequestException exception) { logger.LogWarning(exception, "Scheduling resource activation change rejected."); TempData["ErrorMessage"] = exception.Message; }
        return RedirectToAction(nameof(Index));
    }
    private async Task<IActionResult> Editor(Guid? uid, SaveSchedulingResourceRequest request, CancellationToken token)
    {
        try
        {
            var providers = (await client.ProvidersAsync(token)).ToList();
            var current = uid.HasValue ? await client.GetAsync(uid.Value, token) : null;
            if (current?.ProviderUid is Guid providerUid && providers.All(x => x.ProviderUid != providerUid))
                providers.Add(new(providerUid, current.ProviderDisplayName + " (inactive; existing link)"));
            return View("Edit", new SchedulingResourceEditModel(uid, request, providers, current?.ProviderUid.HasValue == true));
        }
        catch (HttpRequestException exception) { return LoadFailure(exception); }
    }
    private IActionResult LoadFailure(HttpRequestException exception)
    {
        logger.LogWarning(exception, "Scheduling resource editor could not be loaded.");
        return StatusCode((int?)exception.StatusCode ?? 503, new { message = "Scheduling resource information could not be loaded. Verify your access and try again later." });
    }
}
public sealed record SchedulingResourceIndexModel(string Status, IReadOnlyList<SchedulingResourceItem> Resources);
public sealed record SchedulingResourceEditModel(Guid? ResourceUid, SaveSchedulingResourceRequest Resource,
    IReadOnlyList<SchedulingProviderChoice> Providers, bool ProviderLinkLocked);
