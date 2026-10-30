using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Api.Authorization;
using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.Scheduling;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/admin/scheduling-resources")]
[RequirePermission(PermissionKeys.SchedulingManage)]
[RequirePermission(PermissionKeys.ProvidersView)]
public sealed class SchedulingResourcesController(SchedulingResourceAdministrationService service,
    ILogger<SchedulingResourcesController> logger) : ControllerBase
{
    [HttpGet]
    public Task<ActionResult<IReadOnlyList<SchedulingResourceItem>>> List(string status = "All", CancellationToken token = default) =>
        Execute<IReadOnlyList<SchedulingResourceItem>>(async () => Ok(await service.ListAsync(status, token)));
    [HttpGet("providers")]
    public Task<ActionResult<IReadOnlyList<SchedulingProviderChoice>>> Providers(CancellationToken token) =>
        Execute<IReadOnlyList<SchedulingProviderChoice>>(async () => Ok(await service.ProvidersAsync(token)));
    [HttpGet("{uid:guid}")]
    public Task<ActionResult<SchedulingResourceItem>> Get(Guid uid, CancellationToken token) =>
        Execute<SchedulingResourceItem>(async () => await service.GetAsync(uid, token) is { } item ? Ok(item) : NotFound());
    [HttpPost]
    public Task<ActionResult<SchedulingResourceItem>> Create(SaveSchedulingResourceRequest request, CancellationToken token) =>
        Execute<SchedulingResourceItem>(async () => await service.SaveAsync(null, request, ClinicalUserActorContext.GetRequired(HttpContext), token) is { } item
            ? CreatedAtAction(nameof(Get), new { uid = item.ResourceUid }, item) : NotFound());
    [HttpPut("{uid:guid}")]
    public Task<ActionResult<SchedulingResourceItem>> Update(Guid uid, SaveSchedulingResourceRequest request, CancellationToken token) =>
        Execute<SchedulingResourceItem>(async () => await service.SaveAsync(uid, request, ClinicalUserActorContext.GetRequired(HttpContext), token) is { } item ? Ok(item) : NotFound());
    [HttpPost("{uid:guid}/activate")]
    public Task<ActionResult<SchedulingResourceItem>> Activate(Guid uid, SchedulingResourceVersionRequest request, CancellationToken token) => SetActive(uid, true, request, token);
    [HttpPost("{uid:guid}/deactivate")]
    public Task<ActionResult<SchedulingResourceItem>> Deactivate(Guid uid, SchedulingResourceVersionRequest request, CancellationToken token) => SetActive(uid, false, request, token);
    private Task<ActionResult<SchedulingResourceItem>> SetActive(Guid uid, bool active, SchedulingResourceVersionRequest request, CancellationToken token) =>
        Execute<SchedulingResourceItem>(async () => await service.SetActiveAsync(uid, active, request.RowVersion, ClinicalUserActorContext.GetRequired(HttpContext), token) is { } item ? Ok(item) : NotFound());
    private async Task<ActionResult<T>> Execute<T>(Func<Task<ActionResult>> action)
    {
        try { return await action(); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (SchedulingResourceConflictException exception) { return Conflict(new { message = exception.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scheduling resource administration failed. TraceIdentifier: {TraceIdentifier}.", HttpContext.TraceIdentifier);
            return Problem(statusCode: 503, title: "Scheduling resources unavailable", detail: "The resource operation could not be completed. Try again later.");
        }
    }
}
