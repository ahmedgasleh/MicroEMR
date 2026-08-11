using System.ComponentModel.DataAnnotations;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.Templates.Definitions;

namespace MicroEMR.Application.Templates.Contracts;

public sealed class CreateAdministrativeTemplateRequest
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [Required] public string TemplateKind { get; set; } = "Document";
    [Required, StringLength(100)] public string Category { get; set; } = string.Empty;
    [Required] public string TemplateScope { get; set; } = "Clinic";
    public long? OwnerUserId { get; set; }
    [Required] public TemplateDefinition Definition { get; set; } = new() { SchemaVersion=1, Sections=[] };
}

public sealed class UpdateAdministrativeTemplateMetadataRequest
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [Required] public string TemplateKind { get; set; } = "Document";
    [Required, StringLength(100)] public string Category { get; set; } = string.Empty;
    [Required] public string TemplateScope { get; set; } = "Clinic";
    public long? OwnerUserId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class CloneDocumentTemplateRequest
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [Required] public string TemplateScope { get; set; } = "Clinic";
    public long? OwnerUserId { get; set; }
    public Guid? SourceTemplateVersionUid { get; set; }
}

public sealed class SetAdministrativeTemplateActiveRequest
{
    public bool IsActive { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class TemplateAdministrationResult
{
    public DocumentTemplateDetailsResponse Template { get; init; } = new();
    public DocumentTemplateVersionResponse? DraftVersion { get; init; }
}

public readonly record struct TemplateAccessContext(long UserId, bool IsClinicAdministrator);
