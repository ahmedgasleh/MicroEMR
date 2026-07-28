using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.EncounterSoapTemplates;

public sealed class EncounterSoapTemplateResponse
{
    public Guid EncounterSoapTemplateUid { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? EncounterType { get; set; }
    public string? SubjectiveTemplate { get; set; }
    public string? ObjectiveTemplate { get; set; }
    public string? AssessmentTemplate { get; set; }
    public string? PlanTemplate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public string? UpdatedByDisplayName { get; set; }
    public string? RowVersion { get; set; }
}

public class SaveEncounterSoapTemplateRequest
{
    [Required, StringLength(200)] public string TemplateName { get; set; } = string.Empty;
    [StringLength(100)] public string? EncounterType { get; set; }
    public string? SubjectiveTemplate { get; set; }
    public string? ObjectiveTemplate { get; set; }
    public string? AssessmentTemplate { get; set; }
    public string? PlanTemplate { get; set; }
}
public sealed class CreateEncounterSoapTemplateRequest : SaveEncounterSoapTemplateRequest { }
public sealed class UpdateEncounterSoapTemplateRequest : SaveEncounterSoapTemplateRequest { }
public sealed class SetEncounterSoapTemplateActiveRequest { public bool IsActive { get; set; } }
