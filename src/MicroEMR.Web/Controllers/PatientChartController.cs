using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Services.Patients;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class PatientChartController : Controller
{
    private readonly ICurrentPatientContext _currentPatientContext;
    private readonly IPatientApiClient _patientApiClient;
    private readonly ILogger<PatientChartController> _logger;

    public PatientChartController(
        ICurrentPatientContext currentPatientContext,
        IPatientApiClient patientApiClient,
        ILogger<PatientChartController> logger)
    {
        _currentPatientContext = currentPatientContext;
        _patientApiClient = patientApiClient;
        _logger = logger;
    }

    [HttpGet("/PatientChart")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var patientUid = _currentPatientContext.GetPatientUid();
        if (!patientUid.HasValue)
        {
            return View();
        }

        try
        {
            var patient = await _patientApiClient.GetByUidAsync(patientUid.Value, cancellationToken);
            if (patient is not null)
            {
                return RedirectToAction("Details", "Patients", new { patientUid = patientUid.Value });
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Remembered patient {PatientUid} is no longer accessible.", patientUid);
        }

        _currentPatientContext.Clear();
        return View();
    }
}
