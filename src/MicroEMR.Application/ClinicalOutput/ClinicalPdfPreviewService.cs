using System.Diagnostics;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.PatientEncounters.Repositories;
using MicroEMR.Application.Patients.Services;
using MicroEMR.Application.Templates.Output;
using MicroEMR.Application.Templates.Runtime;
using MicroEMR.Application.Templates.Serialization;
using MicroEMR.Application.Templates.Variables;
using Microsoft.Extensions.Logging;

namespace MicroEMR.Application.ClinicalOutput;

public sealed record TemplatePreviewRequest(string? StructuredDataJson);

public interface IClinicalPdfPreviewService
{
    Task<byte[]?> PreviewPatientDocumentAsync(Guid documentUid, TemplatePreviewRequest request, CancellationToken token = default);
    Task<byte[]?> PreviewEncounterAsync(Guid encounterUid, TemplatePreviewRequest request, CancellationToken token = default);
}

public sealed class ClinicalPdfPreviewService(
    IPatientDocumentRepository documents,
    IPatientEncounterRepository encounters,
    IDocumentTemplateVersionRepository versions,
    IPatientService patients,
    ITemplateDefinitionSerializer definitions,
    ITemplateInstanceRuntime runtime,
    ITemplateOutputBuilder outputBuilder,
    ITemplateHtmlRenderer htmlRenderer,
    IClinicalPrintLayoutRenderer printLayout,
    IPdfRenderer pdfRenderer,
    TimeProvider timeProvider,
    ILogger<ClinicalPdfPreviewService> logger) : IClinicalPdfPreviewService
{
    public async Task<byte[]?> PreviewPatientDocumentAsync(Guid documentUid, TemplatePreviewRequest request, CancellationToken token = default)
    {
        var document = await documents.GetByUidAsync(documentUid, token);
        if (document is null) return null;
        if (!document.TemplateUid.HasValue || !document.TemplateVersionUid.HasValue || document.StructuredDataJson is null)
            throw new InvalidOperationException("PDF preview is available only for schema-driven patient documents.");
        var version = await versions.GetByUidAsync(document.TemplateVersionUid.Value, token)
            ?? throw new InvalidOperationException("The document's historical template version is unavailable.");
        EnsureProvenance(document.TemplateUid.Value, version.TemplateUid);
        var definition = RequireDefinition(version.DefinitionJson);
        var data = ProcessDraft(definition, request.StructuredDataJson);
        var patient = await patients.GetByUidAsync(document.PatientUid, token)
            ?? throw new InvalidOperationException("The document patient is unavailable.");
        var context = new TemplateVariableContext(patient.FullName, patient.DateOfBirth, document.CreatedByDisplayName,
            document.CreatedAt, DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
        return await RenderAsync(document.Title, definition, data, context, document.DocumentUid, null,
            document.TemplateUid.Value, version.TemplateVersionUid, token);
    }

    public async Task<byte[]?> PreviewEncounterAsync(Guid encounterUid, TemplatePreviewRequest request, CancellationToken token = default)
    {
        var encounter = await encounters.GetByUidAsync(encounterUid, token);
        if (encounter is null) return null;
        if (!string.Equals(encounter.Status, "Open", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only open encounters can preview submitted draft values.");
        if (!encounter.TemplateUid.HasValue || !encounter.TemplateVersionUid.HasValue || encounter.StructuredDataJson is null)
            throw new InvalidOperationException("PDF preview is available only for schema-driven encounters.");
        var version = await versions.GetByUidAsync(encounter.TemplateVersionUid.Value, token)
            ?? throw new InvalidOperationException("The encounter's historical template version is unavailable.");
        EnsureProvenance(encounter.TemplateUid.Value, version.TemplateUid);
        var definition = RequireDefinition(version.DefinitionJson);
        var data = ProcessDraft(definition, request.StructuredDataJson);
        var patient = await patients.GetByUidAsync(encounter.PatientUid, token)
            ?? throw new InvalidOperationException("The encounter patient is unavailable.");
        var context = new TemplateVariableContext(patient.FullName, patient.DateOfBirth,
            encounter.ProviderName ?? encounter.CreatedByDisplayName, encounter.EncounterDateUtc,
            DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
        return await RenderAsync(encounter.TemplateName ?? encounter.EncounterType, definition, data, context, null,
            encounter.EncounterUid, encounter.TemplateUid.Value, version.TemplateVersionUid, token);
    }

    private TemplateInstanceData ProcessDraft(Templates.Definitions.TemplateDefinition definition, string? json)
    {
        var result = runtime.Process(definition, json);
        var blocking = result.Errors.Where(error => error.Code != "Required").ToArray();
        if (blocking.Length > 0) throw new TemplateInstanceValidationException(blocking);
        if (result.Data is not null) return result.Data;
        // Full processing intentionally drops data when only completeness errors exist.
        var optionalDefinition = CloneWithoutRequired(definition);
        var draft = runtime.Process(optionalDefinition, json);
        if (!draft.IsValid) throw new TemplateInstanceValidationException(draft.Errors);
        return draft.Data!;
    }

    private async Task<byte[]> RenderAsync(string title, Templates.Definitions.TemplateDefinition definition,
        TemplateInstanceData data, TemplateVariableContext? context, Guid? documentUid, Guid? encounterUid,
        Guid templateUid, Guid versionUid, CancellationToken token)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            var html = htmlRenderer.Render(outputBuilder.Build(definition, data, context));
            return await pdfRenderer.RenderAsync(printLayout.Render(title, html), token);
        }
        catch (Exception exception) when (exception is not TemplateInstanceValidationException)
        {
            logger.LogError(exception,
                "PDF preview failed for document {DocumentUid}, encounter {EncounterUid}, template {TemplateUid}, version {TemplateVersionUid}, schema {SchemaVersion}.",
                documentUid, encounterUid, templateUid, versionUid, definition.SchemaVersion);
            throw;
        }
        finally
        {
            logger.LogInformation(
                "PDF preview render finished for document {DocumentUid}, encounter {EncounterUid}, template {TemplateUid}, version {TemplateVersionUid} in {ElapsedMilliseconds} ms.",
                documentUid, encounterUid, templateUid, versionUid, timer.ElapsedMilliseconds);
        }
    }

    private Templates.Definitions.TemplateDefinition RequireDefinition(string json)
    {
        var result = definitions.Process(json);
        if (!result.IsValid) throw new TemplateInstanceValidationException(result.Errors
            .Select(error => new TemplateInstanceValidationError(error.Path, error.Code, error.Message)).ToArray());
        return result.Definition!;
    }

    private static Templates.Definitions.TemplateDefinition CloneWithoutRequired(Templates.Definitions.TemplateDefinition source)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(source);
        var clone = System.Text.Json.JsonSerializer.Deserialize<Templates.Definitions.TemplateDefinition>(json)!;
        foreach (var field in clone.Sections!.SelectMany(section => section.Fields!)) field.Required = false;
        return clone;
    }

    private static void EnsureProvenance(Guid templateUid, Guid versionTemplateUid)
    {
        if (templateUid != versionTemplateUid)
            throw new InvalidOperationException("The template version does not belong to the clinical record template.");
    }
}
