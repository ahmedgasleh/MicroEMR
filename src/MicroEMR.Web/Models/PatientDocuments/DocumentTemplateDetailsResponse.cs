namespace MicroEMR.Web.Models.PatientDocuments;

public sealed class DocumentTemplateDetailsResponse
{
    public Guid TemplateUid { get; set; }

    public string TemplateName { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string TemplateContent { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
