using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.Patients;
using MicroEMR.Web.Services.Patients;
using MicroEMR.Web.Services.PatientDocuments;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class PatientsController : Controller
{
    private readonly IPatientApiClient _patientApiClient;
    private readonly IPatientDocumentApiClient _patientDocumentApiClient;
    private readonly ILogger<PatientsController> _logger;

    public PatientsController(
        IPatientApiClient patientApiClient,
        IPatientDocumentApiClient patientDocumentApiClient,
        ILogger<PatientsController> logger)
    {
        _patientApiClient = patientApiClient;
        _logger = logger;
        _patientDocumentApiClient = patientDocumentApiClient;
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
public async Task<IActionResult> Details(
    Guid patientUid,
    string? tab,
    CancellationToken cancellationToken)
{
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

    var model = new PatientChartViewModel
    {
        Patient = patient,
        Documents = documents,
        ActiveTab = string.Equals(
            tab,
            "documents",
            StringComparison.OrdinalIgnoreCase)
                ? "documents"
                : "demographics"
    };

    return View(model);
}
}