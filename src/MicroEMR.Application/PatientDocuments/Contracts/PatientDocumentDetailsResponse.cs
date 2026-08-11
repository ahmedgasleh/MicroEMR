namespace MicroEMR.Application.PatientDocuments.Contracts;

using MicroEMR.Application.Templates.Definitions;

public sealed class PatientDocumentDetailsResponse
{
    public Guid DocumentUid { get; set; }

    public Guid PatientUid { get; set; }

    public Guid? TemplateUid { get; set; }
    public Guid? TemplateVersionUid { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
    public string? StructuredDataJson { get; set; }
    public bool IsStructured => StructuredDataJson is not null;
    public TemplateDefinition? TemplateDefinition { get; set; }
    public string? TemplateName { get; set; }
    public int? TemplateVersionNumber { get; set; }

    public long? CreatedBy { get; set; }

    public string? CreatedByDisplayName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string RowVersion { get; set; } = string.Empty;

    public string ContentRowVersion { get; set; } = string.Empty;
}
