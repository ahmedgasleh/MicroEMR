using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Reflection.Emit;
using System.Security.Cryptography.Xml;

using MicroEMR.Api.Contracts.Patients;
using MicroEMR.Api.Services.Patients;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Route("api/patients")]
//[Authorize]
[AllowAnonymous] // For development only. Remove this attribute in production.
public sealed class PatientsController : ControllerBase
{
    private readonly IPatientService _patientService;

    public PatientsController (
        IPatientService patientService )
    {
        _patientService = patientService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(PatientSearchResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PatientSearchResponse>> Search (
        [FromQuery] string? searchText,
        [FromQuery] DateOnly? dateOfBirth,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default )
    {
        var result =
            await _patientService.SearchAsync(
                searchText,
                dateOfBirth,
                pageNumber,
                pageSize,
                includeInactive,
                cancellationToken);

        return Ok(result);
    }

    [HttpGet("{patientUid:guid}")]
    [ProducesResponseType(
        typeof(PatientDetailsResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PatientDetailsResponse>> GetByUid (
        Guid patientUid,
        CancellationToken cancellationToken = default )
    {
        var patient =
            await _patientService.GetByUidAsync(
                patientUid,
                cancellationToken);

        if (patient is null)
        {
            return NotFound(
                new
                {
                    message = "Patient was not found."
                });
        }

        return Ok(patient);
    }

    [HttpPost]
    [ProducesResponseType(
        typeof(PatientDetailsResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PatientDetailsResponse>> Create (
        [FromBody] CreatePatientRequest request,
        CancellationToken cancellationToken = default )
    {
        long? createdBy = null;

        // Later, resolve the authenticated user's "sub" claim
        // to MicroEMR_Db.ApplicationUser.UserId.

        var patient =
            await _patientService.CreateAsync(
                request,
                createdBy,
                cancellationToken);

        return CreatedAtAction(
            nameof(GetByUid),
            new
            {
                patientUid = patient.PatientUid
            },
            patient);
    }
}