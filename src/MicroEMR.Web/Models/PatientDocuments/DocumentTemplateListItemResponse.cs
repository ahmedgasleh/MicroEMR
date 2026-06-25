namespace MicroEMR.Web.Models.PatientDocuments;

public sealed class DocumentTemplateListItemResponse
{
    public Guid TemplateUid { get; set; }

    public string TemplateName { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }
}
