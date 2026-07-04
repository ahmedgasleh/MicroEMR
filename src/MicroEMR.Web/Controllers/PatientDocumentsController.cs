using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.PatientDocuments;
using MicroEMR.Web.Services.PatientDocuments;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class PatientDocumentsController : Controller
{
    private readonly IPatientDocumentApiClient _documentApiClient;
    private readonly ILogger<PatientDocumentsController> _logger;

    public PatientDocumentsController(
        IPatientDocumentApiClient documentApiClient,
        ILogger<PatientDocumentsController> logger)
    {
        _documentApiClient = documentApiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Create(
        Guid patientUid,
        CancellationToken cancellationToken)
    {
        if (patientUid == Guid.Empty)
        {
            return BadRequest();
        }

        var model = new CreatePatientDocumentViewModel
        {
            PatientUid = patientUid,
            Templates = await LoadTemplatesAsync(cancellationToken)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreatePatientDocumentViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.PatientUid == Guid.Empty)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            model.Templates = await LoadTemplatesAsync(cancellationToken);

            return View(model);
        }

        var request = new CreatePatientDocumentRequest
        {
            TemplateUid = model.TemplateUid,
            DocumentType = model.DocumentType,
            Title = model.Title,
            Content = model.Content
        };

        try
        {
            // Prototype only: production must enforce template authorization
            // and sanitize document content server-side before persistence.
            await _documentApiClient.CreateAsync(
                model.PatientUid,
                request,
                cancellationToken);

            TempData["SuccessMessage"] =
                "Document saved as draft.";

            return RedirectToAction(
                "Details",
                "Patients",
                new
                {
                    patientUid = model.PatientUid,
                    tab = "documents"
                });
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogWarning(
                exception,
                "The document API rejected the create document request for patient {PatientUid}.",
                model.PatientUid);

            AddApiValidationErrors(
                exception.Message,
                "The document could not be saved. Review the document fields and try again.");

            model.Templates = await LoadTemplatesAsync(cancellationToken);

            return View(model);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "Unable to create document for patient {PatientUid}.",
                model.PatientUid);

            ModelState.AddModelError(
                string.Empty,
                "The document could not be saved. Please try again.");

            model.Templates = await LoadTemplatesAsync(cancellationToken);

            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetTemplate(
        Guid templateUid,
        CancellationToken cancellationToken)
    {
        if (templateUid == Guid.Empty)
        {
            return BadRequest();
        }

        var template =
            await _documentApiClient.GetTemplateByUidAsync(
                templateUid,
                cancellationToken);

        if (template is null)
        {
            return NotFound();
        }

        return Json(new
        {
            template.TemplateUid,
            template.TemplateName,
            template.DocumentType,
            template.TemplateContent
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid documentUid,
        CancellationToken cancellationToken)
    {
        if (documentUid == Guid.Empty)
        {
            return BadRequest();
        }

        var document =
            await _documentApiClient.GetByUidAsync(
                documentUid,
                cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        return View(document);
    }

    private async Task<IReadOnlyList<DocumentTemplateListItemResponse>>
        LoadTemplatesAsync(
            CancellationToken cancellationToken)
    {
        var templates =
            await _documentApiClient.GetActiveTemplatesAsync(
                cancellationToken);

        return templates ?? Array.Empty<DocumentTemplateListItemResponse>();
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
