using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.PatientEncounters;
using MicroEMR.Web.Services.PatientEncounters;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class PatientEncountersController : Controller
{
    private readonly IPatientEncounterApiClient _encounterApiClient;
    private readonly ILogger<PatientEncountersController> _logger;

    public PatientEncountersController(
        IPatientEncounterApiClient encounterApiClient,
        ILogger<PatientEncountersController> logger)
    {
        _encounterApiClient = encounterApiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid encounterUid,
        CancellationToken cancellationToken)
    {
        if (encounterUid == Guid.Empty)
        {
            return BadRequest();
        }

        PatientEncounterDetailsResponse? encounter;

        try
        {
            encounter =
                await _encounterApiClient.GetByUidAsync(
                    encounterUid,
                    cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to load encounter {EncounterUid} because the API rejected the access token.",
                encounterUid);

            TempData["WarningMessage"] =
                "The encounter could not be loaded. Sign in again or restart the API service.";

            return RedirectToAction(
                "Search",
                "Patients");
        }

        if (encounter is null)
        {
            return NotFound();
        }

        return View(encounter);
    }

    [HttpGet]
    public async Task<IActionResult> EncounterDetails(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || encounterUid == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                message = "Encounter details could not be loaded."
            });
        }

        try
        {
            var encounter =
                await _encounterApiClient.GetByUidAsync(
                    encounterUid,
                    cancellationToken);

            if (encounter is null || encounter.PatientUid != patientUid)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Encounter was not found."
                });
            }

            return Json(new
            {
                success = true,
                encounter
            });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Encounter details could not be loaded from the API.");

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    success = false,
                    message = "Encounter details could not be loaded."
                });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEncounterNote(
        UpdateEncounterNoteViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty || model.EncounterUid == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                message = "Encounter note could not be saved."
            });
        }

        try
        {
            var encounter = await _encounterApiClient.UpdateNoteAsync(
                model.PatientUid,
                model.EncounterUid,
                new UpdateEncounterNoteRequest
                {
                    Notes = model.Notes
                },
                cancellationToken);

            if (encounter is null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Encounter was not found."
                });
            }

            return Json(new
            {
                success = true,
                message = "Encounter note saved.",
                notes = encounter.Notes,
                updatedAt = encounter.UpdatedAt
            });
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            return Conflict(new
            {
                success = false,
                message = "Encounter note cannot be edited."
            });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Encounter note could not be saved.");

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    success = false,
                    message = "Encounter note could not be saved."
                });
        }
    }

    [HttpGet]
    public IActionResult Create(
        Guid patientUid)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        return View(new CreatePatientEncounterViewModel
        {
            PatientUid = patientUid,
            EncounterDateLocal = DateTime.Now
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreatePatientEncounterViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty)
        {
            return BadRequest();
        }

        if (model.EncounterDateLocal == default)
        {
            ModelState.AddModelError(
                nameof(model.EncounterDateLocal),
                "Encounter date/time is required.");
        }

        if (string.IsNullOrWhiteSpace(model.EncounterType))
        {
            ModelState.AddModelError(
                nameof(model.EncounterType),
                "Encounter type is required.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new CreatePatientEncounterRequest
        {
            EncounterDateUtc =
                DateTime.SpecifyKind(
                    model.EncounterDateLocal,
                    DateTimeKind.Local)
                .ToUniversalTime(),
            EncounterType = model.EncounterType,
            ReasonForVisit = model.ReasonForVisit,
            LocationName = model.LocationName,
            ProviderName = model.ProviderName
        };

        try
        {
            await _encounterApiClient.CreateAsync(
                model.PatientUid,
                request,
                cancellationToken);

            TempData["SuccessMessage"] =
                "Encounter created successfully.";

            return RedirectToAction(
                "Details",
                "Patients",
                new
                {
                    patientUid = model.PatientUid,
                    tab = "encounters"
                });
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogWarning(
                exception,
                "The encounter API rejected the create encounter request for patient {PatientUid}.",
                model.PatientUid);

            AddApiValidationErrors(
                exception.Message,
                "The encounter could not be created. Review the fields and try again.");

            return View(model);
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                exception,
                "Patient {PatientUid} was not found while creating an encounter.",
                model.PatientUid);

            return NotFound();
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to create encounter for patient {PatientUid} because the API rejected the access token.",
                model.PatientUid);

            ModelState.AddModelError(
                string.Empty,
                "The encounter could not be created because the API rejected the access token. Sign in again or restart the API service.");

            return View(model);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "Unable to create encounter for patient {PatientUid}.",
                model.PatientUid);

            ModelState.AddModelError(
                string.Empty,
                "The encounter could not be created. Please try again.");

            return View(model);
        }
    }

    private void AddApiValidationErrors(
        string responseBody,
        string fallbackMessage)
    {
        if (TryAddValidationProblemErrors(responseBody))
        {
            return;
        }

        ModelState.AddModelError(
            string.Empty,
            fallbackMessage);
    }

    private bool TryAddValidationProblemErrors(
        string responseBody)
    {
        try
        {
            var jsonStart = responseBody.IndexOf('{');

            if (jsonStart < 0)
            {
                return false;
            }

            using var document = JsonDocument.Parse(
                responseBody[jsonStart..]);

            if (!document.RootElement.TryGetProperty(
                    "errors",
                    out var errors)
                || errors.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var addedError = false;

            foreach (var property in errors.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var error in property.Value.EnumerateArray())
                {
                    if (error.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    ModelState.AddModelError(
                        property.Name,
                        error.GetString() ?? "The value is invalid.");

                    addedError = true;
                }
            }

            return addedError;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
