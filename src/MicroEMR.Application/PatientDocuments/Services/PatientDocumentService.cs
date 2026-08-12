using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.Templates.Contracts;
using MicroEMR.Application.Templates.Runtime;
using MicroEMR.Application.Templates.Serialization;
using MicroEMR.Application.Templates.Services;
using MicroEMR.Application.Templates.Output;
using MicroEMR.Application.Templates.Variables;

namespace MicroEMR.Application.PatientDocuments.Services;

public sealed class PatientDocumentService(
    IPatientDocumentRepository repository,
    IDocumentTemplateVersionRepository versions,
    ITemplateDefinitionSerializer definitions,
    ITemplateInstanceRuntime runtime,
    ITemplateAuthorizationService authorization,
    ITemplateOutputBuilder? outputBuilder = null,
    ITemplateHtmlRenderer? htmlRenderer = null) : IPatientDocumentService
{
    private readonly IPatientDocumentRepository _repository = repository;
    private readonly ITemplateOutputBuilder _outputBuilder = outputBuilder
        ?? new TemplateOutputBuilder(new TemplateVariableResolver());
    private readonly ITemplateHtmlRenderer _htmlRenderer = htmlRenderer ?? new TemplateHtmlRenderer();

    public Task<IReadOnlyList<PatientDocumentListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        return _repository.GetByPatientUidAsync(
            patientUid,
            cancellationToken);
    }

    public async Task<PatientDocumentDetailsResponse?> GetByUidAsync(
        Guid documentUid,
        CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetByUidAsync(documentUid, cancellationToken);
        return document is null ? null : await EnrichAsync(document, cancellationToken);
    }

    public async Task<PatientDocumentDetailsResponse?> UpdateDraftAsync(
        Guid documentUid,
        UpdatePatientDocumentDraftRequest request,
        long updatedBy,
        CancellationToken cancellationToken = default)
    {
        var current = await _repository.GetByUidAsync(documentUid, cancellationToken);
        if (current is null) return null;
        if (current.StructuredDataJson is not null)
        {
            if (!current.TemplateVersionUid.HasValue) throw new InvalidOperationException("The structured document has no template version provenance.");
            var version = await versions.GetByUidAsync(current.TemplateVersionUid.Value, cancellationToken)
                ?? throw new InvalidOperationException("The document's template version is unavailable.");
            EnsureVersionProvenance(current.TemplateUid, version);
            var definition = RequireDefinition(version.DefinitionJson);
            var processed = runtime.Process(definition, request.StructuredDataJson);
            if (!processed.IsValid) throw new TemplateInstanceValidationException(processed.Errors);
            request.StructuredDataJson = processed.Json;
            request.Content = _htmlRenderer.Render(_outputBuilder.Build(definition, processed.Data!));
        }
        else request.StructuredDataJson = null;
        var saved = await _repository.UpdateDraftAsync(documentUid, request, updatedBy, cancellationToken);
        return saved is null ? null : await EnrichAsync(saved, cancellationToken);
    }

    public Task<IReadOnlyList<DocumentTemplateListItemResponse>>
        GetActiveTemplatesAsync(
            CancellationToken cancellationToken = default)
    {
        return _repository.GetActiveTemplatesAsync(cancellationToken);
    }

    public Task<DocumentTemplateDetailsResponse?> GetTemplateByUidAsync(
        Guid templateUid,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetTemplateByUidAsync(
            templateUid,
            cancellationToken);
    }

    public Task<IReadOnlyList<DocumentTemplateDetailsResponse>> GetTemplatesAsync(string statusFilter, CancellationToken cancellationToken = default) =>
        _repository.GetTemplatesAsync(NormalizeStatus(statusFilter), cancellationToken);

    public Task<DocumentTemplateDetailsResponse?> CreateTemplateAsync(CreateDocumentTemplateRequest request, long? createdBy, CancellationToken cancellationToken = default) =>
        _repository.CreateTemplateAsync(request, createdBy, cancellationToken);

    public Task<DocumentTemplateDetailsResponse?> UpdateTemplateAsync(Guid templateUid, UpdateDocumentTemplateRequest request, long? updatedBy, CancellationToken cancellationToken = default) =>
        _repository.UpdateTemplateAsync(templateUid, request, updatedBy, cancellationToken);

    public Task<DocumentTemplateDetailsResponse?> SetTemplateActiveAsync(Guid templateUid, bool isActive, long? updatedBy, CancellationToken cancellationToken = default) =>
        _repository.SetTemplateActiveAsync(templateUid, isActive, updatedBy, cancellationToken);

    private static string NormalizeStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "inactive" => "Inactive",
        "all" => "All",
        _ => "Active"
    };

    public async Task<PatientDocumentDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientDocumentRequest request,
        long? createdBy,
        TemplateAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (request.TemplateUid.HasValue)
        {
            var template = await _repository.GetTemplateByUidAsync(request.TemplateUid.Value, cancellationToken)
                ?? throw new UnauthorizedAccessException("The selected template is unavailable.");
            if (!template.IsActive || template.TemplateKind != "Document" || !authorization.CanView(template, accessContext))
                throw new UnauthorizedAccessException("The selected template cannot be used for a patient document.");
            var version = (await versions.GetByTemplateUidAsync(template.TemplateUid, cancellationToken))
                .SingleOrDefault(x => x.IsCurrent && x.Status == "Published")
                ?? throw new InvalidOperationException("The selected template has no active published version.");
            EnsureVersionProvenance(template.TemplateUid, version);
            request.ResolvedTemplateVersionUid = version.TemplateVersionUid;
            request.DocumentType = string.IsNullOrWhiteSpace(template.Category) ? template.DocumentType : template.Category;
            if (string.IsNullOrWhiteSpace(request.Title)) request.Title = template.TemplateName;
            var definition = RequireDefinition(version.DefinitionJson);
            var isLegacy = (definition.Sections?.Count ?? 0) == 0 && !string.IsNullOrWhiteSpace(version.TemplateContent);
            if (!isLegacy)
            {
                var initial = runtime.CreateInitial(definition);
                if (!initial.IsValid) throw new TemplateInstanceValidationException(initial.Errors);
                request.StructuredDataJson = initial.Json;
                request.Content = _htmlRenderer.Render(_outputBuilder.Build(definition, initial.Data!));
            }
        }
        else
        {
            request.ResolvedTemplateVersionUid = null;
            request.StructuredDataJson = null;
        }
        var created = await _repository.CreateAsync(patientUid, request, createdBy, cancellationToken);
        return await EnrichAsync(created, cancellationToken);
    }

    private async Task<PatientDocumentDetailsResponse> EnrichAsync(PatientDocumentDetailsResponse document, CancellationToken token)
    {
        if (document.StructuredDataJson is null || !document.TemplateVersionUid.HasValue) return document;
        var version = await versions.GetByUidAsync(document.TemplateVersionUid.Value, token)
            ?? throw new InvalidOperationException("The document's historical template version is unavailable.");
        EnsureVersionProvenance(document.TemplateUid, version);
        document.TemplateDefinition = RequireDefinition(version.DefinitionJson);
        document.TemplateVersionNumber = version.VersionNumber;
        if (document.TemplateUid.HasValue)
            document.TemplateName = (await _repository.GetTemplateByUidAsync(document.TemplateUid.Value, token))?.TemplateName;
        return document;
    }

    private Templates.Definitions.TemplateDefinition RequireDefinition(string json)
    {
        var result = definitions.Process(json);
        if (!result.IsValid) throw new TemplateInstanceValidationException(result.Errors
            .Select(x => new TemplateInstanceValidationError(x.Path, x.Code, x.Message)).ToArray());
        return result.Definition!;
    }

    private static void EnsureVersionProvenance(Guid? templateUid, DocumentTemplateVersionResponse version)
    {
        if (templateUid.HasValue && version.TemplateUid != templateUid.Value)
            throw new InvalidOperationException("The template version does not belong to the document template.");
    }
}
