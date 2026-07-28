using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientDocuments.Contracts;

public sealed class CreateDocumentTemplateRequest
{
    [Required, StringLength(200)] public string TemplateName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string DocumentType { get; set; } = string.Empty;
    public string TemplateContent { get; set; } = string.Empty;
}

public sealed class UpdateDocumentTemplateRequest
{
    [Required, StringLength(200)] public string TemplateName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string DocumentType { get; set; } = string.Empty;
    public string TemplateContent { get; set; } = string.Empty;
}

public sealed class SetDocumentTemplateActiveRequest
{
    public bool IsActive { get; set; }
}
