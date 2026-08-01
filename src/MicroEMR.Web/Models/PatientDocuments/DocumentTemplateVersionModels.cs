namespace MicroEMR.Web.Models.PatientDocuments;

public sealed class DocumentTemplateVersionResponse
{
    public Guid TemplateVersionUid { get; set; }
    public Guid TemplateUid { get; set; }
    public int VersionNumber { get; set; }
    public string TemplateContent { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class UpdateDocumentTemplateVersionRequest
{
    public string TemplateContent { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ChangeDocumentTemplateVersionStatusRequest
{
    public string RowVersion { get; set; } = string.Empty;
}
