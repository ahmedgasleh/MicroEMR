using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.PatientProblems;
using MicroEMR.Web.Services.PatientProblems;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class PatientProblemsController(IPatientProblemApiClient client, ILogger<PatientProblemsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Problems(Guid patientUid, string status = "Active", CancellationToken cancellationToken = default)
    {
        if (patientUid == Guid.Empty) return BadRequest();
        if (!ValidStatus(status)) status = "Active";
        return Json(await client.GetByPatientUidAsync(patientUid, status, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProblem(CreatePatientProblemViewModel model, CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty) return BadRequest(Failure("Problem information is invalid."));
        if (!ModelState.IsValid) return BadRequest(Failure(FirstError("Problem could not be saved.")));
        try
        {
            var problem = await client.CreateAsync(model.PatientUid, new CreatePatientProblemRequest
            { ProblemName = model.ProblemName, ProblemDescription = model.ProblemDescription, OnsetDate = model.OnsetDate }, cancellationToken);
            return Json(new { success = true, message = "Problem saved.", problem });
        }
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Unable to create a patient problem.");
            return StatusCode(StatusCodes.Status502BadGateway, Failure("Problem could not be saved."));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProblem(UpdatePatientProblemViewModel model, CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty || model.PatientProblemUid == Guid.Empty) return BadRequest(Failure("Problem information is invalid."));
        if (!ModelState.IsValid) return BadRequest(Failure(FirstError("Problem could not be saved.")));
        try
        {
            var problem = await client.UpdateAsync(model.PatientUid, model.PatientProblemUid, new UpdatePatientProblemRequest
            { ProblemName = model.ProblemName, ProblemDescription = model.ProblemDescription, OnsetDate = model.OnsetDate }, cancellationToken);
            return problem is null ? NotFound(Failure("Problem was not found.")) : Json(new { success = true, message = "Problem saved.", problem });
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            return Conflict(Failure("Resolved problems cannot be edited."));
        }
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Unable to update a patient problem.");
            return StatusCode(StatusCodes.Status502BadGateway, Failure("Problem could not be saved."));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveProblem(ResolvePatientProblemViewModel model, CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty || model.PatientProblemUid == Guid.Empty) return BadRequest(Failure("Problem information is invalid."));
        if (!ModelState.IsValid) return BadRequest(Failure(FirstError("Problem could not be resolved.")));
        try
        {
            var problem = await client.ResolveAsync(model.PatientUid, model.PatientProblemUid,
                new ResolvePatientProblemRequest { ResolutionReason = model.ResolutionReason }, cancellationToken);
            return problem is null ? NotFound(Failure("Problem was not found.")) : Json(new { success = true, message = "Problem resolved.", problem });
        }
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Unable to resolve a patient problem.");
            return StatusCode(StatusCodes.Status502BadGateway, Failure("Problem could not be resolved."));
        }
    }

    private static bool ValidStatus(string status) => new[] { "Active", "Resolved", "All" }.Contains(status, StringComparer.OrdinalIgnoreCase);
    private object Failure(string message) => new { success = false, message };
    private string FirstError(string fallback) => ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage).FirstOrDefault() ?? fallback;
}
