namespace MicroEMR.Web.Models.PatientDocuments;

public sealed class DocumentTemplateListItemResponse
{
    public Guid TemplateUid { get; set; }

    public string TemplateName { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;
    public string TemplateKind { get; set; } = "Document";
    public string? Category { get; set; }
    public string TemplateScope { get; set; } = "Clinic";

    public string? Description { get; set; }

    public bool IsActive { get; set; }
    public Guid? TemplateVersionUid { get; set; }
    public int? CurrentVersion { get; set; }
}
