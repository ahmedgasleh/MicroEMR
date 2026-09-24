using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientCareTeam;
using MicroEMR.Web.Authorization;
using MicroEMR.Web.Services.PatientCareTeam;
using MicroEMR.Web.Services.Providers;

namespace MicroEMR.Web.Controllers;

[Authorize, RequireWebPermission(PermissionKeys.PatientsView)]
public sealed class PatientCareTeamController(
    IPatientCareTeamApiClient client,
    IProviderAdministrationApiClient providers,
    IWebPermissionService permissions,
    ILogger<PatientCareTeamController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> List(Guid patientUid, CancellationToken token)
    {
        if (patientUid == Guid.Empty) return BadRequest(new { message = "Patient is required." });
        try
        {
            var relationships = await client.List(patientUid, token);
            var canViewProviders = await permissions.HasAsync(PermissionKeys.ProvidersView, token);
            var providerItems = canViewProviders ? await providers.List("All", token) : [];
            return Json(new { relationships, providers = providerItems.Select(x => new { x.ProviderUid, x.DisplayName, x.Specialty, x.IsActive }) });
        }
        catch (HttpRequestException exception) { return Failure(exception); }
    }

    [HttpGet, RequireWebPermission(PermissionKeys.PatientsEdit)]
    public async Task<IActionResult> FormData(Guid patientUid, CancellationToken token)
    {
        if (patientUid == Guid.Empty) return BadRequest(new { message = "Patient is required." });
        if (!await permissions.HasAsync(PermissionKeys.ProvidersView, token)) return Forbid();
        try
        {
            var types = await client.Types(patientUid, token);
            var activeProviders = await providers.List("Active", token);
            return Json(new { types, providers = activeProviders.Select(x => new { x.ProviderUid, x.DisplayName, x.Specialty }) });
        }
        catch (HttpRequestException exception) { return Failure(exception); }
    }

    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.PatientsEdit)]
    public async Task<IActionResult> Add(Guid patientUid, AddPatientCareTeamRelationshipRequest request, CancellationToken token)
    {
        if (!await permissions.HasAsync(PermissionKeys.ProvidersView, token)) return Forbid();
        if (patientUid == Guid.Empty || !ModelState.IsValid || request.ProviderUid == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.RelationshipTypeCode) || request.StartDate == default)
            return BadRequest(new { message = "Provider, role, and start date are required." });
        try { return Json(await client.Add(patientUid, request, token)); }
        catch (HttpRequestException exception) { return Failure(exception); }
    }

    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.PatientsEdit)]
    public async Task<IActionResult> Update(Guid patientUid, Guid relationshipUid, UpdatePatientCareTeamRelationshipRequest request, CancellationToken token)
    {
        if (patientUid == Guid.Empty || relationshipUid == Guid.Empty || !ModelState.IsValid || request.StartDate == default || string.IsNullOrWhiteSpace(request.RowVersion))
            return BadRequest(new { message = "Start date and relationship version are required." });
        try { return Json(await client.Update(patientUid, relationshipUid, request, token)); }
        catch (HttpRequestException exception) { return Failure(exception); }
    }

    [HttpPost, ValidateAntiForgeryToken, RequireWebPermission(PermissionKeys.PatientsEdit)]
    public async Task<IActionResult> End(Guid patientUid, Guid relationshipUid, EndPatientCareTeamRelationshipRequest request, CancellationToken token)
    {
        if (patientUid == Guid.Empty || relationshipUid == Guid.Empty || !ModelState.IsValid || request.EndDate == default || string.IsNullOrWhiteSpace(request.RowVersion))
            return BadRequest(new { message = "End date and relationship version are required." });
        try { return Json(await client.End(patientUid, relationshipUid, request, token)); }
        catch (HttpRequestException exception) { return Failure(exception); }
    }

    private IActionResult Failure(HttpRequestException exception)
    {
        logger.LogWarning(exception, "Care team request failed.");
        var status = exception.StatusCode switch
        {
            HttpStatusCode.BadRequest => 400,
            HttpStatusCode.Forbidden => 403,
            HttpStatusCode.NotFound => 404,
            HttpStatusCode.Conflict => 409,
            _ => 502
        };
        return StatusCode(status, new { message = exception.Message });
    }
}
