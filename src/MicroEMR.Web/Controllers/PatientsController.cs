using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.PatientAllergies;
using MicroEMR.Web.Models.PatientEncounters;
using MicroEMR.Web.Models.PatientDocuments;
using MicroEMR.Web.Models.PatientMedications;
using MicroEMR.Web.Models.PatientProblems;
using MicroEMR.Web.Models.Patients;
using MicroEMR.Web.Services.PatientAllergies;
using MicroEMR.Web.Services.Patients;
using MicroEMR.Web.Services.PatientDocuments;
using MicroEMR.Web.Services.PatientEncounters;
using MicroEMR.Web.Services.PatientMedications;
using MicroEMR.Web.Services.PatientProblems;
using MicroEMR.Web.Services.PatientVitals;
using MicroEMR.Web.Models.PatientVitals;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class PatientsController : Controller
{
    private readonly IPatientApiClient _patientApiClient;
    private readonly IPatientAllergyApiClient _patientAllergyApiClient;
    private readonly IPatientDocumentApiClient _patientDocumentApiClient;
    private readonly IPatientEncounterApiClient _patientEncounterApiClient;
    private readonly IPatientMedicationApiClient _patientMedicationApiClient;
    private readonly IPatientProblemApiClient _patientProblemApiClient;
    private readonly IPatientVitalApiClient _patientVitalApiClient;
    private readonly ILogger<PatientsController> _logger;

    public PatientsController(
        IPatientApiClient patientApiClient,
        IPatientAllergyApiClient patientAllergyApiClient,
        IPatientDocumentApiClient patientDocumentApiClient,
        IPatientEncounterApiClient patientEncounterApiClient,
        IPatientMedicationApiClient patientMedicationApiClient,
        IPatientProblemApiClient patientProblemApiClient,
        IPatientVitalApiClient patientVitalApiClient,
        ILogger<PatientsController> logger)
    {
        _patientApiClient = patientApiClient;
        _patientAllergyApiClient = patientAllergyApiClient;
        _logger = logger;
        _patientDocumentApiClient = patientDocumentApiClient;
        _patientEncounterApiClient = patientEncounterApiClient;
        _patientMedicationApiClient = patientMedicationApiClient;
        _patientProblemApiClient = patientProblemApiClient;
        _patientVitalApiClient = patientVitalApiClient;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Search));
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        string? searchText,
        DateOnly? dateOfBirth,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result =
                await _patientApiClient.SearchAsync(
                    searchText,
                    dateOfBirth,
                    pageNumber,
                    25,
                    cancellationToken);

            ViewBag.SearchText = searchText;
            ViewBag.DateOfBirth = dateOfBirth;

            return View(result);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "Unable to load patients from MicroEMR.Api.");

            ModelState.AddModelError(
                string.Empty,
                "Unable to load patients. Please try again.");

            return View(new PatientSearchResponse
            {
                PageNumber = pageNumber,
                PageSize = 25
            });
        }
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreatePatientRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreatePatientRequest model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var patient =
                await _patientApiClient.CreateAsync(
                    model,
                    cancellationToken);

            TempData["SuccessMessage"] =
                $"Patient {patient.FullName} was registered successfully.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    patientUid = patient.PatientUid
                });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "Unable to create patient.");

            ModelState.AddModelError(
                string.Empty,
                "The patient could not be registered.");

            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        Guid patientUid,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        var patient =
            await _patientApiClient.GetByUidAsync(
                patientUid,
                cancellationToken);

        if (patient is null)
        {
            return NotFound();
        }

        return View(MapEditViewModel(patient));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        EditPatientDemographicsViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _patientApiClient.UpdateDemographicsAsync(
                model.PatientUid,
                MapUpdateRequest(model),
                cancellationToken);

            TempData["SuccessMessage"] =
                "Patient demographics updated successfully.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    patientUid = model.PatientUid,
                    tab = "demographics"
                });
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogWarning(
                exception,
                "The patient API rejected a demographics update for patient {PatientUid}.",
                model.PatientUid);

            ModelState.AddModelError(
                string.Empty,
                "The patient could not be updated. Review the fields and try again.");

            return View(model);
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogWarning(
                exception,
                "Concurrency conflict while updating patient {PatientUid}.",
                model.PatientUid);

            ModelState.AddModelError(
                string.Empty,
                "This patient was updated by another user. Reload the form and try again.");

            return View(model);
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                exception,
                "Patient {PatientUid} was not found during demographics update.",
                model.PatientUid);

            return NotFound();
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "Unable to update patient {PatientUid}.",
                model.PatientUid);

            ModelState.AddModelError(
                string.Empty,
                "The patient could not be updated. Please try again.");

            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid patientUid,
        string? tab,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        var patient =
            await _patientApiClient.GetByUidAsync(
                patientUid,
                cancellationToken);

        if (patient is null)
        {
            return NotFound();
        }

        var documents =
            await _patientDocumentApiClient.GetByPatientUidAsync(
                patientUid,
                cancellationToken);

        var encounters =
            await LoadEncountersForChartAsync(
                patientUid,
                cancellationToken);

        var allergies =
            await LoadAllergiesForChartAsync(
                patientUid,
                cancellationToken);

        var medications =
            await LoadMedicationsForChartAsync(
                patientUid,
                cancellationToken);

        var problems = await LoadProblemsForChartAsync(patientUid, cancellationToken);
        var vitals = await LoadVitalsForChartAsync(patientUid, cancellationToken);

        var model = new PatientChartViewModel
        {
            Summary = BuildSummary(patient, problems, allergies, medications, vitals, encounters, documents),
            Patient = patient,
            Documents = documents,
            Encounters = encounters,
            Allergies = allergies,
            Medications = medications,
            Problems = problems,
            Vitals = vitals,
            ActiveTab = NormalizePatientChartTab(tab)
        };

        return View(model);
    }

    private async Task<IReadOnlyList<PatientMedicationListItemResponse>>
        LoadMedicationsForChartAsync(
            Guid patientUid,
            CancellationToken cancellationToken)
    {
        try
        {
            return await _patientMedicationApiClient.GetByPatientUidAsync(
                patientUid,
                cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to load medications for patient {PatientUid} because the API rejected the access token.",
                patientUid);

            TempData["WarningMessage"] =
                "Medications could not be loaded. Sign in again or restart the API service.";

            return Array.Empty<PatientMedicationListItemResponse>();
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning(
                exception,
                "Unable to load medications for patient {PatientUid} because the API returned unauthorized.",
                patientUid);

            TempData["WarningMessage"] =
                "Medications could not be loaded. Sign in again or restart the API service.";

            return Array.Empty<PatientMedicationListItemResponse>();
        }
    }

    private async Task<IReadOnlyList<PatientProblemViewModel>> LoadProblemsForChartAsync(
        Guid patientUid, CancellationToken cancellationToken)
    {
        try
        {
            return await _patientProblemApiClient.GetByPatientUidAsync(patientUid, "All", cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Unable to load problems for patient {PatientUid}.", patientUid);
            TempData["WarningMessage"] = "Problems could not be loaded. Sign in again or restart the API service.";
            return Array.Empty<PatientProblemViewModel>();
        }
    }

    private async Task<IReadOnlyList<PatientVitalViewModel>> LoadVitalsForChartAsync(Guid patientUid, CancellationToken cancellationToken)
    {
        try { return await _patientVitalApiClient.GetPatientVitalsAsync(patientUid, cancellationToken); }
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        { _logger.LogWarning(exception, "Unable to load vitals for patient {PatientUid}.", patientUid); TempData["WarningMessage"] = "Vitals could not be loaded. Sign in again or restart the API service."; return Array.Empty<PatientVitalViewModel>(); }
    }

    private async Task<IReadOnlyList<PatientAllergyListItemResponse>>
        LoadAllergiesForChartAsync(
            Guid patientUid,
            CancellationToken cancellationToken)
    {
        try
        {
            return await _patientAllergyApiClient.GetByPatientUidAsync(
                patientUid,
                cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to load allergies for patient {PatientUid} because the API rejected the access token.",
                patientUid);

            TempData["WarningMessage"] =
                "Allergies could not be loaded. Sign in again or restart the API service.";

            return Array.Empty<PatientAllergyListItemResponse>();
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning(
                exception,
                "Unable to load allergies for patient {PatientUid} because the API returned unauthorized.",
                patientUid);

            TempData["WarningMessage"] =
                "Allergies could not be loaded. Sign in again or restart the API service.";

            return Array.Empty<PatientAllergyListItemResponse>();
        }
    }

    private async Task<IReadOnlyList<PatientEncounterListItemResponse>>
        LoadEncountersForChartAsync(
            Guid patientUid,
            CancellationToken cancellationToken)
    {
        try
        {
            return await _patientEncounterApiClient.GetByPatientUidAsync(
                patientUid,
                cancellationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to load encounters for patient {PatientUid} because the API rejected the access token.",
                patientUid);

            TempData["WarningMessage"] =
                "Encounters could not be loaded. Sign in again or restart the API service.";

            return Array.Empty<PatientEncounterListItemResponse>();
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning(
                exception,
                "Unable to load encounters for patient {PatientUid} because the API returned unauthorized.",
                patientUid);

            TempData["WarningMessage"] =
                "Encounters could not be loaded. Sign in again or restart the API service.";

            return Array.Empty<PatientEncounterListItemResponse>();
        }
    }

    private static string NormalizePatientChartTab(
        string? tab)
    {
        return tab?.ToLowerInvariant() switch
        {
            "summary" => "summary",
            "allergies" => "allergies",
            "documents" => "documents",
            "encounters" => "encounters",
            "medications" => "medications",
            "problems" => "problems",
            "vitals" => "vitals",
            _ => "summary"
        };
    }

    private static PatientChartSummaryViewModel BuildSummary(
        PatientDetailsResponse patient,
        IReadOnlyList<PatientProblemViewModel> problems,
        IReadOnlyList<PatientAllergyListItemResponse> allergies,
        IReadOnlyList<PatientMedicationListItemResponse> medications,
        IReadOnlyList<PatientVitalViewModel> vitals,
        IReadOnlyList<PatientEncounterListItemResponse> encounters,
        IReadOnlyList<PatientDocumentListItemResponse> documents)
    {
        var activeProblems = problems.Where(item => string.Equals(item.ProblemStatus, "Active", StringComparison.OrdinalIgnoreCase)).ToList();
        var activeAllergies = allergies.Where(item => string.Equals(item.Status, "Active", StringComparison.OrdinalIgnoreCase)).ToList();
        var activeMedications = medications.Where(item => string.Equals(item.Status, "Active", StringComparison.OrdinalIgnoreCase)).ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - patient.DateOfBirth.Year;
        if (patient.DateOfBirth > today.AddYears(-age)) age--;
        return new PatientChartSummaryViewModel
        {
            PatientUid = patient.PatientUid, PatientDisplayName = patient.FullName, PreferredName = patient.PreferredName,
            ChartNumber = patient.ChartNumber, HealthCardNumber = patient.HealthCardNumber, DateOfBirth = patient.DateOfBirth,
            AgeDisplay = $"{age} years", SexAtBirth = patient.SexAtBirth, GenderIdentity = patient.GenderIdentity,
            PhoneNumber = patient.PhoneNumber, Email = patient.Email,
            ActiveProblems = activeProblems.Take(5).ToList(), ActiveProblemsTotalCount = activeProblems.Count,
            ActiveAllergies = activeAllergies.Take(5).ToList(), ActiveAllergiesTotalCount = activeAllergies.Count,
            ActiveMedications = activeMedications.Take(5).ToList(), ActiveMedicationsTotalCount = activeMedications.Count,
            LatestVitals = vitals.OrderByDescending(item => item.RecordedAt).FirstOrDefault(),
            RecentEncounters = encounters.OrderByDescending(item => item.EncounterDateUtc).Take(5).ToList(), RecentEncountersTotalCount = encounters.Count,
            RecentDocuments = documents.OrderByDescending(item => item.CreatedAt).Take(5).ToList(), RecentDocumentsTotalCount = documents.Count
        };
    }

    private static EditPatientDemographicsViewModel MapEditViewModel(
        PatientDetailsResponse patient)
    {
        return new EditPatientDemographicsViewModel
        {
            PatientUid = patient.PatientUid,
            ChartNumber = patient.ChartNumber,
            FirstName = patient.FirstName,
            MiddleName = patient.MiddleName,
            LastName = patient.LastName,
            PreferredName = patient.PreferredName,
            DateOfBirth = patient.DateOfBirth,
            SexAtBirth = patient.SexAtBirth,
            GenderIdentity = patient.GenderIdentity,
            HealthCardNumber = patient.HealthCardNumber,
            HealthCardVersion = patient.HealthCardVersion,
            PhoneNumber = patient.PhoneNumber,
            AlternatePhoneNumber = patient.AlternatePhoneNumber,
            Email = patient.Email,
            AddressLine1 = patient.AddressLine1,
            AddressLine2 = patient.AddressLine2,
            City = patient.City,
            Province = patient.Province,
            PostalCode = patient.PostalCode,
            CountryCode = string.IsNullOrWhiteSpace(patient.CountryCode)
                ? "CA"
                : patient.CountryCode,
            IsActive = patient.IsActive,
            RowVersion = patient.RowVersion
        };
    }

    private static UpdatePatientDemographicsRequest MapUpdateRequest(
        EditPatientDemographicsViewModel model)
    {
        return new UpdatePatientDemographicsRequest
        {
            FirstName = model.FirstName,
            MiddleName = model.MiddleName,
            LastName = model.LastName,
            PreferredName = model.PreferredName,
            DateOfBirth = model.DateOfBirth,
            SexAtBirth = model.SexAtBirth,
            GenderIdentity = model.GenderIdentity,
            HealthCardNumber = model.HealthCardNumber,
            HealthCardVersion = model.HealthCardVersion,
            PhoneNumber = model.PhoneNumber,
            AlternatePhoneNumber = model.AlternatePhoneNumber,
            Email = model.Email,
            AddressLine1 = model.AddressLine1,
            AddressLine2 = model.AddressLine2,
            City = model.City,
            Province = model.Province,
            PostalCode = model.PostalCode,
            CountryCode = string.IsNullOrWhiteSpace(model.CountryCode)
                ? "CA"
                : model.CountryCode,
            IsActive = model.IsActive,
            RowVersion = model.RowVersion
        };
    }
}
