namespace MicroEMR.Application.PatientDocuments.Contracts;

public sealed class PatientDocumentDetailsResponse
{
    public Guid DocumentUid { get; set; }

    public Guid PatientUid { get; set; }

    public Guid? TemplateUid { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public long? CreatedBy { get; set; }

    public string? CreatedByDisplayName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}