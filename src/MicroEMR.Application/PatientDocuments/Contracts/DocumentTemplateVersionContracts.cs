using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientDocuments.Contracts;

public sealed class DocumentTemplateVersionResponse
{
    public Guid TemplateVersionUid { get; set; }
    public Guid TemplateUid { get; set; }
    public int VersionNumber { get; set; }
    public string TemplateContent { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public string DefinitionJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public DateTime? PublishedAt { get; set; }
    public long? PublishedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class UpdateDocumentTemplateVersionRequest
{
    public string TemplateContent { get; set; } = string.Empty;

    public int? SchemaVersion { get; set; }

    public string? DefinitionJson { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ChangeDocumentTemplateVersionStatusRequest
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}
