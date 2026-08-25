using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.PatientEncounters;
using MicroEMR.Web.Services.PatientEncounters;
using MicroEMR.Web.Services;
using MicroEMR.Web.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Web.Services.Patients;

namespace MicroEMR.Web.Controllers;

[Authorize]
[RequireWebPermission(PermissionKeys.EncountersView)]
public sealed class PatientEncountersController : Controller
{
    [HttpGet]
    public async Task<IActionResult> FinalPdf(Guid encounterUid, CancellationToken cancellationToken)
    {
        if (encounterUid == Guid.Empty) return BadRequest();
        try { return File(await _encounterApiClient.GetFinalPdfAsync(encounterUid, cancellationToken), "application/pdf"); }
        catch (HttpRequestException exception) when (exception.StatusCode is HttpStatusCode.NotFound) { return NotFound(); }
    }
    [HttpPost, ValidateAntiForgeryToken]
    [RequireWebPermission(PermissionKeys.EncountersEdit)]
    public async Task<IActionResult> PreviewPdf(Guid encounterUid, string structuredDataJson, CancellationToken cancellationToken)
    {
        if (encounterUid == Guid.Empty || string.IsNullOrWhiteSpace(structuredDataJson)) return BadRequest();
        try
        {
            return File(await _encounterApiClient.PreviewPdfAsync(encounterUid, structuredDataJson, cancellationToken), "application/pdf");
        }
        catch (HttpRequestException exception) when (exception.StatusCode is HttpStatusCode.BadRequest)
        {
            return BadRequest(new { message = "Preview could not be generated. Review the template field values." });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "PDF preview is temporarily unavailable." });
        }
    }
    private readonly IPatientEncounterApiClient _encounterApiClient;
    private readonly IPatientApiClient _patientApiClient;
    private readonly ILogger<PatientEncountersController> _logger;

    public PatientEncountersController(
        IPatientEncounterApiClient encounterApiClient,
        IPatientApiClient patientApiClient,
        ILogger<PatientEncountersController> logger)
    {
        _encounterApiClient = encounterApiClient;
        _patientApiClient = patientApiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> PrintHistory(
        Guid patientUid,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || startDate == default || endDate == default)
            return BadRequest("Patient, start date, and end date are required.");
        if (endDate < startDate)
            return BadRequest("End date cannot be before start date.");

        var patient = await _patientApiClient.GetByUidAsync(patientUid, cancellationToken);
        if (patient is null) return NotFound();

        var encounters = await _encounterApiClient.GetByPatientUidAsync(patientUid, cancellationToken);
        return View(EncounterHistoryPrintViewModel.Create(patient, encounters, startDate, endDate));
    }

    [HttpGet]
    public async Task<IActionResult> EncounterTemplates(CancellationToken cancellationToken) =>
        Json(await _encounterApiClient.GetEncounterTemplatesAsync(cancellationToken));

    [HttpGet]
    [SensitiveCapability(SecurityAuditCapabilities.EncounterView)]
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

    [HttpGet]
    public async Task<IActionResult> EncounterHistory(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || encounterUid == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                message = "Encounter history could not be loaded."
            });
        }

        try
        {
            var history = await _encounterApiClient.GetEncounterHistoryAsync(
                patientUid, encounterUid, cancellationToken);
            return Json(new { success = true, history });
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
                "Encounter history could not be loaded for encounter {EncounterUid}.",
                encounterUid);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                message = "Encounter history could not be loaded."
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> EncounterAddendums(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || encounterUid == Guid.Empty)
            return BadRequest(new { success = false, message = "Encounter addendums could not be loaded." });

        try
        {
            var addendums = await _encounterApiClient.GetEncounterAddendumsAsync(
                patientUid, encounterUid, cancellationToken);
            return Json(new { success = true, addendums });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Encounter addendums could not be loaded for encounter {EncounterUid}.", encounterUid);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { success = false, message = "Encounter addendums could not be loaded." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireWebPermission(PermissionKeys.EncountersEdit)]
    public async Task<IActionResult> CreateEncounterAddendum(
        CreateEncounterAddendumViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty || model.EncounterUid == Guid.Empty)
            return BadRequest(new { success = false, message = "Addendum could not be saved." });
        if (string.IsNullOrWhiteSpace(model.AddendumText))
            return BadRequest(new { success = false, message = "Addendum text is required." });

        try
        {
            var addendum = await _encounterApiClient.CreateEncounterAddendumAsync(
                model.PatientUid,
                model.EncounterUid,
                new CreateEncounterAddendumRequest { AddendumText = model.AddendumText.Trim() },
                cancellationToken);
            return addendum is null
                ? NotFound(new { success = false, message = "Encounter was not found." })
                : Json(new { success = true, message = "Addendum saved.", addendum });
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            return Conflict(new
            {
                success = false,
                message = "Addendum can only be added to a signed encounter."
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Encounter addendum could not be saved for encounter {EncounterUid}.", model.EncounterUid);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { success = false, message = "Addendum could not be saved." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireWebPermission(PermissionKeys.EncountersEdit)]
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireWebPermission(PermissionKeys.EncountersEdit)]
    public async Task<IActionResult> UpdateEncounterSoapNote(
        UpdateEncounterSoapNoteViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty || model.EncounterUid == Guid.Empty)
            return BadRequest(new { success = false, message = "Encounter note could not be saved." });

        try
        {
            var encounter = await _encounterApiClient.UpdateEncounterSoapNoteAsync(
                model.PatientUid,
                model.EncounterUid,
                new UpdateEncounterSoapNoteRequest
                {
                    SubjectiveNote = model.SubjectiveNote,
                    ObjectiveNote = model.ObjectiveNote,
                    AssessmentNote = model.AssessmentNote,
                    PlanNote = model.PlanNote
                },
                cancellationToken);
            return encounter is null
                ? NotFound(new { success = false, message = "Encounter was not found." })
                : Json(new { success = true, message = "Encounter note saved.", encounter });
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            return Conflict(new { success = false, message = "Signed encounter notes cannot be edited." });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Encounter SOAP note could not be saved for encounter {EncounterUid}.", model.EncounterUid);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { success = false, message = "Encounter note could not be saved." });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequireWebPermission(PermissionKeys.EncountersEdit)]
    public async Task<IActionResult> UpdateEncounterStructuredData(
        Guid patientUid, Guid encounterUid, string structuredDataJson, string rowVersion,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || encounterUid == Guid.Empty) return BadRequest();
        try
        {
            var encounter = await _encounterApiClient.UpdateStructuredDataAsync(patientUid, encounterUid,
                new UpdateEncounterStructuredDataRequest { StructuredDataJson = structuredDataJson, RowVersion = rowVersion }, cancellationToken);
            return encounter is null ? NotFound() : Json(new { success = true, message = "Encounter draft saved.", encounter });
        }
        catch (HttpRequestException exception) when (exception.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            return StatusCode((int)exception.StatusCode.Value, new { success = false, message = "Review the template fields and refresh if the encounter changed." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireWebPermission(PermissionKeys.EncountersSign)]
    public async Task<IActionResult> SignEncounter(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || encounterUid == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                message = "Encounter could not be signed."
            });
        }

        try
        {
            var encounter = await _encounterApiClient.SignEncounterAsync(
                patientUid,
                encounterUid,
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
                message = "Encounter signed.",
                encounter
            });
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            return Conflict(new
            {
                success = false,
                message = "Encounter cannot be signed."
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
                "Encounter could not be signed.");

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    success = false,
                    message = "Encounter could not be signed."
                });
        }
    }

    [HttpGet]
    [RequireWebPermission(PermissionKeys.EncountersEdit)]
    public async Task<IActionResult> Create(
        Guid patientUid, CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        return View(new CreatePatientEncounterViewModel
        {
            PatientUid = patientUid,
            EncounterDateLocal = DateTime.Now,
            EncounterTemplates = await _encounterApiClient.GetEncounterTemplatesAsync(cancellationToken)
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
            model.EncounterTemplates = await _encounterApiClient.GetEncounterTemplatesAsync(cancellationToken);
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
            ProviderName = model.ProviderName,
            TemplateUid = model.TemplateUid
        };

        try
        {
            var created = await _encounterApiClient.CreateAsync(
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
                    tab = string.Equals(model.ReturnTab, "summary", StringComparison.OrdinalIgnoreCase)
                        ? "summary"
                        : "encounters",
                    openEncounterUid = created.EncounterUid
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
                SafeApiResponseException.ValidationBody(exception),
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
