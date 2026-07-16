using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.PatientAllergies;
using MicroEMR.Web.Services.PatientAllergies;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class PatientAllergiesController : Controller
{
    private readonly IPatientAllergyApiClient _allergyApiClient;
    private readonly ILogger<PatientAllergiesController> _logger;

    public PatientAllergiesController(
        IPatientAllergyApiClient allergyApiClient,
        ILogger<PatientAllergiesController> logger)
    {
        _allergyApiClient = allergyApiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid allergyUid,
        CancellationToken cancellationToken)
    {
        if (allergyUid == Guid.Empty)
        {
            return BadRequest();
        }

        PatientAllergyDetailsResponse? allergy;

        try
        {
            allergy =
                await _allergyApiClient.GetByUidAsync(
                    allergyUid,
                    cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to load allergy {AllergyUid} because the API rejected the access token.",
                allergyUid);

            TempData["WarningMessage"] =
                "The allergy could not be loaded. Sign in again or restart the API service.";

            return RedirectToAction(
                "Search",
                "Patients");
        }

        if (allergy is null)
        {
            return NotFound();
        }

        return View(allergy);
    }

    [HttpGet]
    public IActionResult Create(
        Guid patientUid)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        return View(new CreatePatientAllergyViewModel
        {
            PatientUid = patientUid
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreatePatientAllergyViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(model.AllergenName))
        {
            ModelState.AddModelError(
                nameof(model.AllergenName),
                "Allergen name is required.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new CreatePatientAllergyRequest
        {
            AllergenName = model.AllergenName,
            AllergenType = model.AllergenType,
            Reaction = model.Reaction,
            Severity = model.Severity,
            OnsetDate = model.OnsetDate,
            Notes = model.Notes
        };

        try
        {
            await _allergyApiClient.CreateAsync(
                model.PatientUid,
                request,
                cancellationToken);

            TempData["SuccessMessage"] =
                "Allergy added successfully.";

            return RedirectToAction(
                "Details",
                "Patients",
                new
                {
                    patientUid = model.PatientUid,
                    tab = "allergies"
                });
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogWarning(
                exception,
                "The allergy API rejected the create allergy request for patient {PatientUid}.",
                model.PatientUid);

            AddApiValidationErrors(
                exception.Message,
                "The allergy could not be added. Review the fields and try again.");

            return View(model);
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                exception,
                "Patient {PatientUid} was not found while creating an allergy.",
                model.PatientUid);

            return NotFound();
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to create allergy for patient {PatientUid} because the API rejected the access token.",
                model.PatientUid);

            ModelState.AddModelError(
                string.Empty,
                "The allergy could not be added because the API rejected the access token. Sign in again or restart the API service.");

            return View(model);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "Unable to create allergy for patient {PatientUid}.",
                model.PatientUid);

            ModelState.AddModelError(
                string.Empty,
                "The allergy could not be added. Please try again.");

            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid allergyUid, CancellationToken cancellationToken)
    {
        if (allergyUid == Guid.Empty)
            return BadRequest();

        var allergy = await _allergyApiClient.GetByUidAsync(allergyUid, cancellationToken);
        if (allergy is null)
            return NotFound();

        return View(new EditPatientAllergyViewModel
        {
            PatientUid = allergy.PatientUid,
            AllergyUid = allergy.AllergyUid,
            AllergenName = allergy.AllergenName,
            AllergenType = allergy.AllergenType,
            Reaction = allergy.Reaction,
            Severity = allergy.Severity,
            OnsetDate = allergy.OnsetDate,
            Status = allergy.Status,
            Notes = allergy.Notes,
            RowVersion = allergy.RowVersion
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        EditPatientAllergyViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty || model.AllergyUid == Guid.Empty)
            return BadRequest();
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var allergy = await _allergyApiClient.UpdateAsync(
                model.PatientUid,
                model.AllergyUid,
                new UpdatePatientAllergyRequest
                {
                    AllergenName = model.AllergenName,
                    AllergenType = model.AllergenType,
                    Reaction = model.Reaction,
                    Severity = model.Severity,
                    OnsetDate = model.OnsetDate,
                    Status = model.Status,
                    Notes = model.Notes,
                    RowVersion = model.RowVersion
                },
                cancellationToken);

            if (allergy is null)
                return NotFound();

            TempData["SuccessMessage"] = "Allergy updated successfully.";
            return RedirectToAction("Details", "Patients", new
            {
                patientUid = model.PatientUid,
                tab = "allergies"
            });
        }
        catch (HttpRequestException exception) when (
            exception.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            _logger.LogWarning(exception, "The allergy API rejected an update request.");
            AddApiValidationErrors(
                exception.Message,
                exception.StatusCode == HttpStatusCode.Conflict
                    ? "The allergy was changed by another user. Reload and try again."
                    : "The allergy could not be updated. Review the fields and try again.");
            return View(model);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(exception, "The allergy API rejected the access token during update.");
            ModelState.AddModelError(string.Empty, "The allergy could not be updated. Sign in again.");
            return View(model);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Unable to update allergy.");
            ModelState.AddModelError(string.Empty, "The allergy could not be updated. Please try again.");
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
