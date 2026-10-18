using System.Net;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Web.Authorization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.PatientAllergies;
using MicroEMR.Web.Services.PatientAllergies;
using MicroEMR.Web.Services;

namespace MicroEMR.Web.Controllers;

[Authorize,RequireWebPermission(PermissionKeys.PatientsView)]
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
        Guid allergyUid, Guid patientUid,
        CancellationToken cancellationToken)
    {
        if (allergyUid == Guid.Empty || patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        PatientAllergyDetailsResponse? allergy;

        try
        {
            allergy =
                await _allergyApiClient.GetForPatientAsync(
                    patientUid, allergyUid,
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

    [HttpGet,RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public async Task<IActionResult> Create(
        Guid patientUid, CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        var state = await _allergyApiClient.GetDocumentationStateAsync(patientUid, cancellationToken);
        return View(new CreatePatientAllergyViewModel
        {
            PatientUid = patientUid,
            NoKnownAllergiesActive = state?.State == "ExplicitlyNone",
            ExpectedAssertionUid=state?.NoKnownAllergies?.AssertionUid,ExpectedAssertionRowVersion=state?.NoKnownAllergies?.RowVersion
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ClinicalDataManage)]
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
            ClinicalFields = model.ClinicalFieldsIncluded ? model.ClinicalFields ?? new() : model.ClinicalFields,
            Notes = model.Notes
            ,ConfirmReplaceNoKnownAllergies = model.ConfirmReplaceNoKnownAllergies,ExpectedAssertionUid=model.ExpectedAssertionUid,ExpectedAssertionRowVersion=model.ExpectedAssertionRowVersion
        };

        if(model.OnsetDateSpecified)request.OnsetDate=model.OnsetDate;
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
                    tab = string.Equals(model.ReturnTab, "summary", StringComparison.OrdinalIgnoreCase)
                        ? "summary"
                        : "allergies"
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
                SafeApiResponseException.ValidationBody(exception),
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
        catch(HttpRequestException exception) when(exception.StatusCode==HttpStatusCode.Conflict)
        {
            _logger.LogWarning(exception,"The reviewed NKA assertion changed before allergy creation.");
            ModelState.AddModelError(string.Empty,"The NKA assertion changed. Reload and review before confirming replacement again.");
            return View(model);
        }
        catch (HttpRequestException exception) when(exception.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized) { return StatusCode((int)exception.StatusCode.Value); }
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

    [HttpPost, ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public async Task<IActionResult> AssertNoKnownAllergies(Guid patientUid, CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty) return BadRequest();
        try
        {
            await _allergyApiClient.AssertNoKnownAllergiesAsync(patientUid, cancellationToken);
            TempData["SuccessMessage"] = "No Known Allergies documented.";
        }
        catch (HttpRequestException exception) when(exception.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized) { return StatusCode((int)exception.StatusCode.Value); }
        catch (HttpRequestException exception) { _logger.LogWarning(exception, "Unable to document NKA for {PatientUid}.", patientUid); TempData["WarningMessage"] = "No Known Allergies could not be documented."; }
        return RedirectToAction("Details", "Patients", new { patientUid, tab = "allergies" });
    }

    [HttpPost, ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public async Task<IActionResult> RevokeNoKnownAllergies(Guid patientUid, Guid assertionUid, string rowVersion, string? reason, CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty || string.IsNullOrWhiteSpace(rowVersion)) return BadRequest();
        try
        {
            await _allergyApiClient.RevokeNoKnownAllergiesAsync(patientUid,
                new RevokeNoKnownAllergiesRequest { AssertionUid=assertionUid,RowVersion = rowVersion, Reason = reason }, cancellationToken);
            TempData["SuccessMessage"] = "No Known Allergies revoked.";
        }
        catch (HttpRequestException exception) when(exception.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized) { return StatusCode((int)exception.StatusCode.Value); }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Unable to revoke NKA for {PatientUid}.", patientUid);
            TempData["WarningMessage"] = exception.StatusCode==HttpStatusCode.Conflict?"The NKA assertion changed. Reload and review before revoking again.":"No Known Allergies could not be revoked.";
        }
        return RedirectToAction("Details", "Patients", new { patientUid, tab = "allergies" });
    }

    [HttpGet,RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public async Task<IActionResult> Edit(Guid allergyUid, Guid patientUid, CancellationToken cancellationToken)
    {
        if (allergyUid == Guid.Empty || patientUid == Guid.Empty)
            return BadRequest();

        var allergy = await _allergyApiClient.GetForPatientAsync(patientUid, allergyUid, cancellationToken);
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
            ClinicalFields=allergy.ClinicalFields,ClinicalFieldsIncluded=true,
            Status = allergy.Status,
            Notes = allergy.Notes,
            RowVersion = allergy.RowVersion
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ClinicalDataManage)]
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
            var request=new UpdatePatientAllergyRequest{AllergenName=model.AllergenName,AllergenType=model.AllergenType,Reaction=model.Reaction,Severity=model.Severity,
                ClinicalFields=model.ClinicalFieldsIncluded?model.ClinicalFields??new():model.ClinicalFields,Status=model.Status,Notes=model.Notes,RowVersion=model.RowVersion};
            if(model.OnsetDateSpecified)request.OnsetDate=model.OnsetDate;
            var allergy=await _allergyApiClient.UpdateAsync(model.PatientUid,model.AllergyUid,request,cancellationToken);

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
                SafeApiResponseException.ValidationBody(exception),
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
        catch (HttpRequestException exception) when(exception.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized) { return StatusCode((int)exception.StatusCode.Value); }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Unable to update allergy.");
            ModelState.AddModelError(string.Empty, "The allergy could not be updated. Please try again.");
            return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken,RequireWebPermission(PermissionKeys.ClinicalDataManage)]
    public async Task<IActionResult> ResolveAllergy(ResolveAllergyViewModel model, CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty || model.AllergyUid == Guid.Empty)
            return BadRequest(new { success = false, message = "Allergy information is invalid." });
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = "Please correct the allergy information." });
        try {
            var result = await _allergyApiClient.ResolveAsync(model.PatientUid, model.AllergyUid,
                new ResolvePatientAllergyRequest { RowVersion=model.RowVersion,ResolveReason = model.ResolveReason }, cancellationToken);
            return result is null ? NotFound(new { success = false, message = "Allergy was not found." })
                : Json(new { success = true, message = "Allergy resolved." });
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested){throw;}
        catch (Exception exception) {
            _logger.LogError(exception, "Unable to resolve allergy.");
            var status=exception is HttpRequestException h && h.StatusCode is {} code?(int)code:exception is UnauthorizedAccessException?401:502;
            return StatusCode(status,new {success=false,message=status==409?"The allergy changed. Reload and review before resolving.":"Allergy could not be resolved."});
        }
    }

    public override void OnActionExecuted(Microsoft.AspNetCore.Mvc.Filters.ActionExecutedContext context)
    {
        if(context.Exception is HttpRequestException error && error.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        { context.Result=StatusCode((int)error.StatusCode.Value);context.ExceptionHandled=true; }
        base.OnActionExecuted(context);
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
