using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.PatientMedications;
using MicroEMR.Web.Services.PatientMedications;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class PatientMedicationsController : Controller
{
    private readonly IPatientMedicationApiClient _medicationApiClient;
    private readonly ILogger<PatientMedicationsController> _logger;

    public PatientMedicationsController(
        IPatientMedicationApiClient medicationApiClient,
        ILogger<PatientMedicationsController> logger)
    {
        _medicationApiClient = medicationApiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid medicationUid,
        CancellationToken cancellationToken)
    {
        if (medicationUid == Guid.Empty)
        {
            return BadRequest();
        }

        PatientMedicationDetailsResponse? medication;

        try
        {
            medication =
                await _medicationApiClient.GetByUidAsync(
                    medicationUid,
                    cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to load medication {MedicationUid} because the API rejected the access token.",
                medicationUid);

            TempData["WarningMessage"] =
                "The medication could not be loaded. Sign in again or restart the API service.";

            return RedirectToAction(
                "Search",
                "Patients");
        }

        if (medication is null)
        {
            return NotFound();
        }

        return View(medication);
    }

    [HttpGet]
    public IActionResult Create(
        Guid patientUid)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        return View(new CreatePatientMedicationViewModel
        {
            PatientUid = patientUid
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreatePatientMedicationViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(model.MedicationName))
        {
            ModelState.AddModelError(
                nameof(model.MedicationName),
                "Medication name is required.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new CreatePatientMedicationRequest
        {
            MedicationName = model.MedicationName,
            Strength = model.Strength,
            DosageForm = model.DosageForm,
            Route = model.Route,
            Directions = model.Directions,
            Frequency = model.Frequency,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            Indication = model.Indication,
            PrescriberName = model.PrescriberName,
            Notes = model.Notes
        };

        try
        {
            await _medicationApiClient.CreateAsync(
                model.PatientUid,
                request,
                cancellationToken);

            TempData["SuccessMessage"] =
                "Medication added successfully.";

            return RedirectToAction(
                "Details",
                "Patients",
                new
                {
                    patientUid = model.PatientUid,
                    tab = "medications"
                });
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogWarning(
                exception,
                "The medication API rejected the create medication request for patient {PatientUid}.",
                model.PatientUid);

            AddApiValidationErrors(
                exception.Message,
                "The medication could not be added. Review the fields and try again.");

            return View(model);
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                exception,
                "Patient {PatientUid} was not found while creating a medication.",
                model.PatientUid);

            return NotFound();
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to create medication for patient {PatientUid} because the API rejected the access token.",
                model.PatientUid);

            ModelState.AddModelError(
                string.Empty,
                "The medication could not be added because the API rejected the access token. Sign in again or restart the API service.");

            return View(model);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "Unable to create medication for patient {PatientUid}.",
                model.PatientUid);

            ModelState.AddModelError(
                string.Empty,
                "The medication could not be added. Please try again.");

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
