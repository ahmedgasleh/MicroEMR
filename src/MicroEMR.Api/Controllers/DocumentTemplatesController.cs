using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.PatientDocuments;
using MicroEMR.Application.Templates.Contracts;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Application.Templates.Serialization;
using MicroEMR.Application.Templates.Services;
using System.Text.Json;
using MicroEMR.Application.ClinicalUsers;

namespace MicroEMR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/document-templates")]
public sealed class DocumentTemplatesController : ControllerBase
{
    private readonly IPatientDocumentService _documentService;
    private readonly ITemplateDefinitionSerializer _definitionSerializer;
    private readonly ITemplateAuthorizationService _templateAuthorization;
    private readonly IAuthorizationService _authorization;
    private readonly IAuthenticatedClinicalUserAccessor _clinicalUsers;

    public DocumentTemplatesController(
        IPatientDocumentService documentService,
        ITemplateDefinitionSerializer definitionSerializer,
        ITemplateAuthorizationService templateAuthorization,
        IAuthorizationService authorization,
        IAuthenticatedClinicalUserAccessor clinicalUsers)
    {
        _documentService = documentService;
        _definitionSerializer = definitionSerializer;
        _templateAuthorization = templateAuthorization;
        _authorization = authorization;
        _clinicalUsers = clinicalUsers;
    }

    [HttpPost("definition/validate")]
    [ProducesResponseType<ValidateTemplateDefinitionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidateTemplateDefinitionResponse>(StatusCodes.Status400BadRequest)]
    public ActionResult<ValidateTemplateDefinitionResponse> ValidateDefinition([FromBody] JsonElement definition)
    {
        var processed = _definitionSerializer.Process(definition.GetRawText());
        var response = new ValidateTemplateDefinitionResponse
        {
            IsValid = processed.IsValid,
            Definition = processed.Definition,
            Errors = processed.Errors
        };
        return processed.IsValid ? Ok(response) : BadRequest(response);
    }

    [HttpGet]
    [ProducesResponseType<
        IReadOnlyList<DocumentTemplateListItemResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplates(
        [FromQuery(Name = "status")] string status = "Active",
        CancellationToken cancellationToken = default)
    {
        var templates = await _documentService.GetTemplatesAsync(status, cancellationToken);

        var context = await AccessContext();
        return Ok(templates.Where(x => x.TemplateKind == "Document" && x.TemplateVersionUid.HasValue
            && (x.TemplateScope != "Personal" || x.OwnerUserId == context.UserId || context.IsClinicAdministrator)));
    }

    [HttpGet("encounter")]
    public async Task<IActionResult> GetEncounterTemplates(CancellationToken cancellationToken)
    {
        var templates = await _documentService.GetTemplatesAsync("Active", cancellationToken);
        var context = await AccessContext();
        return Ok(templates.Where(x => x.TemplateKind == "Encounter" && x.IsActive
            && x.TemplateVersionUid.HasValue
            && (x.TemplateScope != "Personal" || x.OwnerUserId == context.UserId || context.IsClinicAdministrator)));
    }

    [HttpGet("{templateUid:guid}")]
    [ProducesResponseType<DocumentTemplateDetailsResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentTemplateDetailsResponse>>
        GetTemplate(
            Guid templateUid,
            CancellationToken cancellationToken)
    {
        var template =
            await _documentService.GetTemplateByUidAsync(
                templateUid,
                cancellationToken);

        if (template is null)
        {
            return NotFound(new
            {
                message = "The requested template was not found."
            });
        }

        if (!_templateAuthorization.CanView(template, await AccessContext())) return Forbid();

        return Ok(template);
    }

    [Authorize]
    [Authorize(Policy = MicroEMR.Api.Authorization.TenantAuthorizationPolicies.ClinicAdministrator)]
    [HttpPost]
    public async Task<ActionResult<DocumentTemplateDetailsResponse>> CreateTemplate(
        [FromBody] CreateDocumentTemplateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var template = await _documentService.CreateTemplateAsync(request, GetAuthenticatedUserId(), cancellationToken);
        return template is null ? BadRequest() : CreatedAtAction(nameof(GetTemplate), new { templateUid = template.TemplateUid }, template);
    }

    [Authorize]
    [Authorize(Policy = MicroEMR.Api.Authorization.TenantAuthorizationPolicies.ClinicAdministrator)]
    [HttpPut("{templateUid:guid}")]
    public async Task<ActionResult<DocumentTemplateDetailsResponse>> UpdateTemplate(
        Guid templateUid, [FromBody] UpdateDocumentTemplateRequest request, CancellationToken cancellationToken)
    {
        if (templateUid == Guid.Empty || !ModelState.IsValid) return ValidationProblem(ModelState);
        var existing = await _documentService.GetTemplateByUidAsync(templateUid, cancellationToken);
        if (existing is null) return NotFound();
        if (!_templateAuthorization.CanMutate(existing, await AccessContext(true))) return Forbid();
        try
        {
            var template = await _documentService.UpdateTemplateAsync(templateUid, request, GetAuthenticatedUserId(), cancellationToken);
            return template is null ? NotFound() : Ok(template);
        }
        catch (DocumentTemplateVersionConflictException)
        {
            return Conflict(new { message = "Published template content cannot be edited in place. Create a new draft version instead." });
        }
    }

    [Authorize]
    [Authorize(Policy = MicroEMR.Api.Authorization.TenantAuthorizationPolicies.ClinicAdministrator)]
    [HttpPost("{templateUid:guid}/set-active")]
    public async Task<ActionResult<DocumentTemplateDetailsResponse>> SetActive(
        Guid templateUid, [FromBody] SetDocumentTemplateActiveRequest request, CancellationToken cancellationToken)
    {
        if (templateUid == Guid.Empty) return BadRequest();
        var existing = await _documentService.GetTemplateByUidAsync(templateUid, cancellationToken);
        if (existing is null) return NotFound();
        if (!_templateAuthorization.CanMutate(existing, await AccessContext(true))) return Forbid();
        var template = await _documentService.SetTemplateActiveAsync(templateUid, request.IsActive, GetAuthenticatedUserId(), cancellationToken);
        return template is null ? NotFound() : Ok(template);
    }

    private long GetAuthenticatedUserId() =>
        ClinicalUserActorContext.GetRequired(HttpContext);

    private async Task<TemplateAccessContext> AccessContext(bool requireMutationActor = false)
    {
        long userId;
        if (requireMutationActor) userId = GetAuthenticatedUserId();
        else if (!ClinicalUserActorContext.TryGet(HttpContext, out userId))
        {
            try { userId = await _clinicalUsers.GetRequiredUserIdAsync(HttpContext.RequestAborted); }
            catch (ClinicalUserResolutionException) { userId = 0; }
        }
        return new(userId, (await _authorization.AuthorizeAsync(User, null,
            MicroEMR.Api.Authorization.TenantAuthorizationPolicies.ClinicAdministrator)).Succeeded);
    }
}
