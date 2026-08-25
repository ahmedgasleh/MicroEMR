using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.PatientDocuments;
using MicroEMR.Web.Services.PatientDocuments;
using MicroEMR.Web.Services;
using MicroEMR.Web.Authorization;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.SecurityAudit;

namespace MicroEMR.Web.Controllers;

[Authorize]
[RequireWebPermission(PermissionKeys.DocumentsView)]
public sealed class PatientDocumentsController : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    [RequireWebPermission(PermissionKeys.DocumentsManage)]
    public async Task<IActionResult> PreviewPdf(Guid documentUid, string structuredDataJson, CancellationToken cancellationToken)
    {
        if (documentUid == Guid.Empty || string.IsNullOrWhiteSpace(structuredDataJson)) return BadRequest();
        try
        {
            return File(await _documentApiClient.PreviewPdfAsync(documentUid, structuredDataJson, cancellationToken), "application/pdf");
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
    [RequireWebPermission(PermissionKeys.DocumentsManage)]
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
            var created = await _documentApiClient.CreateAsync(
                model.PatientUid,
                request,
                cancellationToken);

            TempData["SuccessMessage"] =
                "Document saved as draft.";

            if (model.TemplateUid.HasValue)
                return RedirectToAction(nameof(Details), new { documentUid = created.DocumentUid });

            return RedirectToAction(
                "Details",
                "Patients",
                new
                {
                    patientUid = model.PatientUid,
                    tab = string.Equals(model.ReturnTab, "summary", StringComparison.OrdinalIgnoreCase)
                        ? "summary"
                        : "documents"
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
                SafeApiResponseException.ValidationBody(exception),
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

        var versions = await _documentApiClient.GetDocumentTemplateVersionsAsync(templateUid, cancellationToken);
        var published = versions.FirstOrDefault(version => version.IsCurrent &&
            string.Equals(version.Status, "Published", StringComparison.OrdinalIgnoreCase));
        var hasSchemaSections = false;
        if (published is not null && !string.IsNullOrWhiteSpace(published.DefinitionJson))
        {
            try
            {
                using var definition = JsonDocument.Parse(published.DefinitionJson);
                hasSchemaSections = definition.RootElement.TryGetProperty("sections", out var sections)
                    && sections.ValueKind == JsonValueKind.Array && sections.GetArrayLength() > 0;
            }
            catch (JsonException) { }
        }
        var isStructured = published is not null &&
            (hasSchemaSections || string.IsNullOrWhiteSpace(published.TemplateContent));

        return Json(new
        {
            template.TemplateUid,
            template.TemplateName,
            template.DocumentType,
            template.Category,
            template.TemplateContent,
            isStructured
        });
    }

    [HttpGet]
    [SensitiveCapability(SecurityAuditCapabilities.PatientDocumentView)]
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireWebPermission(PermissionKeys.DocumentsManage)]
    public async Task<IActionResult> UpdateDraft(
        Guid documentUid,
        UpdatePatientDocumentDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (documentUid == Guid.Empty)
            return BadRequest();

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "The draft could not be saved. Review the document fields and try again.";
            return RedirectToAction(nameof(Details), new { documentUid });
        }

        PatientDocumentDetailsResponse? current = null;
        try
        {
            current = await _documentApiClient.GetByUidAsync(documentUid, cancellationToken);
            if (current?.IsStructured == true && string.IsNullOrWhiteSpace(request.StructuredDataJson))
            {
                TempData["ErrorMessage"] = "The structured form values were not submitted. Reload and try again.";
                return RedirectToAction(nameof(Details), new { documentUid });
            }
            var document = await _documentApiClient.UpdateDraftAsync(
                documentUid,
                request,
                cancellationToken);

            if (document is null)
                return NotFound();

            TempData["SuccessMessage"] = "Document draft updated.";
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogWarning(
                exception,
                "Draft update conflict for patient document {DocumentUid}.",
                documentUid);
            TempData["ErrorMessage"] =
                "This document changed after you opened it. Reloaded values are shown; review them before editing again.";
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogWarning(
                exception,
                "The document API rejected draft update {DocumentUid}.",
                documentUid);
            if (current?.IsStructured == true)
            {
                AddApiValidationErrors(SafeApiResponseException.ValidationBody(exception), "The draft could not be saved. Review the document fields and try again.");
                current.StructuredDataJson = request.StructuredDataJson;
                return View(nameof(Details), current);
            }
            TempData["ErrorMessage"] = "The draft could not be saved. Review the document fields and try again.";
        }

        return RedirectToAction(nameof(Details), new { documentUid });
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
