namespace MicroEMR.Application.PatientDocuments.Contracts;

public sealed class DocumentTemplateDetailsResponse
{
    public Guid TemplateUid { get; set; }

    public string TemplateName { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string TemplateKind { get; set; } = "Document";

    public string? Category { get; set; }

    public string TemplateScope { get; set; } = "Clinic";

    public long? OwnerUserId { get; set; }

    public string? Description { get; set; }

    public string TemplateContent { get; set; } = string.Empty;

    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public string? UpdatedByDisplayName { get; set; }
    public string? RowVersion { get; set; }
    public Guid? TemplateVersionUid { get; set; }
    public int? CurrentVersion { get; set; }
}
