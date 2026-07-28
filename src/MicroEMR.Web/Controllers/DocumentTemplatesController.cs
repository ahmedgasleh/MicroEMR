using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.DocumentTemplates;
using MicroEMR.Web.Models.PatientDocuments;
using MicroEMR.Web.Services.PatientDocuments;

namespace MicroEMR.Web.Controllers;

[Authorize]
public sealed class DocumentTemplatesController : Controller
{
    private readonly IPatientDocumentApiClient _client;
    private readonly ILogger<DocumentTemplatesController> _logger;

    public DocumentTemplatesController(IPatientDocumentApiClient client, ILogger<DocumentTemplatesController> logger)
    { _client = client; _logger = logger; }

    [HttpGet]
    public async Task<IActionResult> Index(string status = "Active", CancellationToken cancellationToken = default)
    {
        status = NormalizeStatus(status);
        var templates = await _client.GetDocumentTemplatesAsync(status, cancellationToken);
        return View(new DocumentTemplateIndexViewModel
        {
            Status = status,
            Templates = templates.Select(x => new DocumentTemplateViewModel
            {
                TemplateUid=x.TemplateUid, TemplateName=x.TemplateName, DocumentType=x.DocumentType,
                TemplateContent=x.TemplateContent, IsActive=x.IsActive, CreatedAt=x.CreatedAt,
                CreatedByDisplayName=x.CreatedByDisplayName, UpdatedAt=x.UpdatedAt,
                UpdatedByDisplayName=x.UpdatedByDisplayName
            }).ToArray()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDocumentTemplateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationJson();
        try
        {
            var result = await _client.CreateDocumentTemplateAsync(ToRequest(model), cancellationToken);
            return Json(new { success = result is not null, message = "Document template created." });
        }
        catch (Exception exception) { return Failure(exception, "create"); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateDocumentTemplateViewModel model, CancellationToken cancellationToken)
    {
        if (model.TemplateUid == Guid.Empty) ModelState.AddModelError(nameof(model.TemplateUid), "Template identifier is required.");
        if (!ModelState.IsValid) return ValidationJson();
        try
        {
            var result = await _client.UpdateDocumentTemplateAsync(model.TemplateUid, ToRequest(model), cancellationToken);
            return result is null ? NotFound(new { success=false, message="Template was not found." }) : Json(new { success=true, message="Document template updated." });
        }
        catch (Exception exception) { return Failure(exception, "update"); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(SetDocumentTemplateActiveViewModel model, CancellationToken cancellationToken)
    {
        if (model.TemplateUid == Guid.Empty) return BadRequest(new { success=false, message="Template identifier is required." });
        try
        {
            var result = await _client.SetDocumentTemplateActiveAsync(model.TemplateUid, model.IsActive, cancellationToken);
            return result is null ? NotFound(new { success=false, message="Template was not found." }) : Json(new { success=true, message=model.IsActive ? "Document template reactivated." : "Document template deactivated." });
        }
        catch (Exception exception) { return Failure(exception, "change status for"); }
    }

    private IActionResult Failure(Exception exception, string action)
    {
        _logger.LogError(exception, "Unable to {Action} a document template.", action);
        return StatusCode(502, new { success=false, message="The document template operation could not be completed." });
    }

    private IActionResult ValidationJson() => BadRequest(new { success=false, message="Please correct the highlighted errors.", errors=ModelState.Where(x=>x.Value?.Errors.Count>0).ToDictionary(x=>x.Key,x=>x.Value!.Errors.Select(e=>e.ErrorMessage).ToArray()) });
    private static SaveDocumentTemplateRequest ToRequest(CreateDocumentTemplateViewModel x) => new() { TemplateName=x.TemplateName, DocumentType=x.DocumentType, TemplateContent=x.TemplateContent };
    private static string NormalizeStatus(string? value) => value?.ToLowerInvariant() switch { "inactive"=>"Inactive", "all"=>"All", _=>"Active" };
}
