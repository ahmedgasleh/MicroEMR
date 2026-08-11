using System.ComponentModel.DataAnnotations;
using MicroEMR.Application.Templates.Definitions;

namespace MicroEMR.Web.Models.TemplateAdministration;

public sealed class TemplateAdministrationIndexViewModel
{
    public IReadOnlyList<TemplateAdministrationListItemViewModel> Templates { get; init; }=[];
    public string Status { get; init; }="All";
    public string Kind { get; init; }="All";
    public string Scope { get; init; }="All";
    public bool CanManageClinic { get; init; }
}

public sealed class TemplateAdministrationListItemViewModel
{
    public Guid TemplateUid { get; init; }
    public string Name { get; init; }=string.Empty;
    public string TemplateKind { get; init; }=string.Empty;
    public string Category { get; init; }=string.Empty;
    public string TemplateScope { get; init; }=string.Empty;
    public long? OwnerUserId { get; init; }
    public bool IsActive { get; init; }
    public int? CurrentVersion { get; init; }
    public string VersionStatus { get; init; }=string.Empty;
    public DateTime LastUpdated { get; init; }
    public string RowVersion { get; init; }=string.Empty;
    public bool CanEdit { get; init; }
}

public sealed class TemplateBuilderViewModel
{
    public TemplateDetailsModel Template { get; init; }=new();
    public TemplateVersionModel Version { get; init; }=new();
    public TemplateDefinition Definition { get; init; }=new(){SchemaVersion=1,Sections=[]};
    public bool IsReadOnly { get; init; }
    public bool CanEdit { get; init; }
    public bool IsEncounterTemplate => Template.TemplateKind=="Encounter";
}

public sealed class TemplateDetailsModel
{
    public Guid TemplateUid { get; set; }
    public string TemplateName { get; set; }=string.Empty;
    public string TemplateKind { get; set; }="Document";
    public string? Category { get; set; }
    public string TemplateScope { get; set; }="Clinic";
    public long? OwnerUserId { get; set; }
    public bool IsActive { get; set; }
    public string? RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CurrentVersion { get; set; }
}

public sealed class TemplateVersionModel
{
    public Guid TemplateVersionUid { get; set; }
    public Guid TemplateUid { get; set; }
    public int VersionNumber { get; set; }
    public int SchemaVersion { get; set; }
    public string DefinitionJson { get; set; }="{\"schemaVersion\":1,\"sections\":[]}";
    public string TemplateContent { get; set; }=string.Empty;
    public string Status { get; set; }=string.Empty;
    public bool IsCurrent { get; set; }
    public string RowVersion { get; set; }=string.Empty;
}

public sealed class CreateTemplateViewModel
{
    [Required,StringLength(200)] public string Name { get; set; }=string.Empty;
    [Required] public string TemplateKind { get; set; }="Document";
    [Required,StringLength(100)] public string Category { get; set; }=string.Empty;
    [Required] public string TemplateScope { get; set; }="Clinic";
}

public sealed class CloneTemplateViewModel
{
    [Required] public Guid TemplateUid { get; set; }
    [Required,StringLength(200)] public string Name { get; set; }=string.Empty;
    [Required] public string TemplateScope { get; set; }="Clinic";
}

public sealed class UpdateTemplateMetadataViewModel
{
    public Guid TemplateUid { get; set; }
    [Required,StringLength(200)] public string Name { get; set; }=string.Empty;
    public string TemplateKind { get; set; }="Document";
    [Required,StringLength(100)] public string Category { get; set; }=string.Empty;
    public string TemplateScope { get; set; }="Clinic";
    public long? OwnerUserId { get; set; }
    public string RowVersion { get; set; }=string.Empty;
}

public sealed class SaveTemplateDefinitionViewModel
{
    public Guid TemplateUid { get; set; }
    public Guid TemplateVersionUid { get; set; }
    public string RowVersion { get; set; }=string.Empty;
    public string TemplateContent { get; set; }=string.Empty;
    public TemplateDefinition Definition { get; set; }=new(){SchemaVersion=1,Sections=[]};
}

public sealed class PublishTemplateViewModel
{
    public Guid TemplateUid { get; set; }
    public Guid TemplateVersionUid { get; set; }
    public string RowVersion { get; set; }=string.Empty;
}

public sealed class SetTemplateActiveViewModel
{
    public Guid TemplateUid { get; set; }
    public bool IsActive { get; set; }
    public string RowVersion { get; set; }=string.Empty;
}

public sealed class TemplateValidationResponseModel
{
    public bool IsValid { get; set; }
    public TemplateDefinition? Definition { get; set; }
    public IReadOnlyList<TemplateValidationErrorModel> Errors { get; set; }=[];
}
public sealed class TemplateValidationErrorModel { public string Path { get; set; }=string.Empty; public string Code { get; set; }=string.Empty; public string Message { get; set; }=string.Empty; }
public sealed class TemplateAdministrationResultModel { public TemplateDetailsModel Template { get; set; }=new(); public TemplateVersionModel? DraftVersion { get; set; } }
