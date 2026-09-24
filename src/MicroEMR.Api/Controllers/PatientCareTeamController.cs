using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.PatientCareTeam;

namespace MicroEMR.Api.Controllers;

[ApiController, Authorize, Route("api/patients/{patientUid:guid}/care-team")]
[RequirePermission(PermissionKeys.PatientsView)]
public sealed class PatientCareTeamController(IPatientCareTeamService service, ILogger<PatientCareTeamController> logger) : ControllerBase
{
    [HttpGet]
    public Task<ActionResult<IReadOnlyList<PatientCareTeamRelationship>>> List(Guid patientUid, CancellationToken token) =>
        Read(async () => await service.ListAsync(patientUid, token));

    [HttpGet("types")]
    public Task<ActionResult<IReadOnlyList<CareTeamRelationshipType>>> Types(Guid patientUid, CancellationToken token) =>
        Read(async () => { await service.ListAsync(patientUid, token); return await service.ListActiveTypesAsync(token); });

    [HttpPost, RequirePermission(PermissionKeys.PatientsEdit)]
    public Task<ActionResult<PatientCareTeamRelationship>> Add(Guid patientUid, AddPatientCareTeamRelationshipRequest request, CancellationToken token) =>
        Write(() => service.AddAsync(patientUid, request, token));

    [HttpPut("{relationshipUid:guid}"), RequirePermission(PermissionKeys.PatientsEdit)]
    public Task<ActionResult<PatientCareTeamRelationship>> Update(Guid patientUid, Guid relationshipUid, UpdatePatientCareTeamRelationshipRequest request, CancellationToken token) =>
        Write(() => service.UpdateAsync(patientUid, relationshipUid, request, token));

    [HttpPost("{relationshipUid:guid}/end"), RequirePermission(PermissionKeys.PatientsEdit)]
    public Task<ActionResult<PatientCareTeamRelationship>> End(Guid patientUid, Guid relationshipUid, EndPatientCareTeamRelationshipRequest request, CancellationToken token) =>
        Write(() => service.EndAsync(patientUid, relationshipUid, request, token));

    private async Task<ActionResult<T>> Read<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    private async Task<ActionResult<PatientCareTeamRelationship>> Write(Func<Task<PatientCareTeamRelationship>> action)
    {
        try { return Ok(await action()); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
        catch (SqlException exception) when (exception.Number is 51800 or 51801 or 51802 or 51803 or 51804 or 51805 or 51806 or 2601 or 2627)
        {
            logger.LogWarning(exception, "Care team change was rejected.");
            var message = exception.Number switch
            {
                51802 or 2601 or 2627 => "This active provider relationship already exists.",
                51803 => "A primary provider is already assigned for this role.",
                51805 => "This relationship changed. Reload the page and try again.",
                51806 => "End date cannot precede start date.",
                _ => "The provider relationship is no longer available. Reload the page and try again."
            };
            return Conflict(new { message });
        }
    }
}
