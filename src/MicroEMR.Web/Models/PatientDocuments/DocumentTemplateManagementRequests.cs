namespace MicroEMR.Web.Models.PatientDocuments;

public sealed class SaveDocumentTemplateRequest
{
    public string TemplateName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string TemplateContent { get; set; } = string.Empty;
}

public sealed class SetDocumentTemplateActiveRequest
{
    public bool IsActive { get; set; }
}
