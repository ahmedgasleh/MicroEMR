using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.DocumentTemplates;

public sealed class DocumentTemplateIndexViewModel
{
    public string Status { get; set; } = "Active";
    public IReadOnlyList<DocumentTemplateViewModel> Templates { get; set; } = [];
}

public sealed class DocumentTemplateViewModel
{
    public Guid TemplateUid { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string TemplateContent { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByDisplayName { get; set; }
    public int? CurrentVersion { get; set; }
}

public sealed class UpdateDocumentTemplateVersionViewModel
{
    [Required] public Guid TemplateUid { get; set; }
    [Required] public Guid TemplateVersionUid { get; set; }
    public string TemplateContent { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ChangeDocumentTemplateVersionStatusViewModel
{
    [Required] public Guid TemplateUid { get; set; }
    [Required] public Guid TemplateVersionUid { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public class CreateDocumentTemplateViewModel
{
    [Required, StringLength(200)] public string TemplateName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string DocumentType { get; set; } = string.Empty;
    public string TemplateContent { get; set; } = string.Empty;
}

public sealed class UpdateDocumentTemplateViewModel : CreateDocumentTemplateViewModel
{
    [Required] public Guid TemplateUid { get; set; }
}

public sealed class SetDocumentTemplateActiveViewModel
{
    [Required] public Guid TemplateUid { get; set; }
    public bool IsActive { get; set; }
}
