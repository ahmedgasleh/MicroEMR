using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MicroEMR.Application.PatientEncounters.Contracts;

public sealed class CreatePatientEncounterRequest
{
    [Required]
    public DateTime EncounterDateUtc { get; set; }

    [Required]
    [StringLength(100)]
    public string EncounterType { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ReasonForVisit { get; set; }

    [StringLength(200)]
    public string? LocationName { get; set; }

    [StringLength(200)]
    public string? ProviderName { get; set; }
    public Guid? EncounterSoapTemplateUid { get; set; }
    public Guid? TemplateUid { get; set; }
    [JsonIgnore] public Guid? ResolvedTemplateVersionUid { get; set; }
    [JsonIgnore] public string? StructuredDataJson { get; set; }
    [JsonIgnore] public string? SubjectiveSnapshot { get; set; }
    [JsonIgnore] public string? ObjectiveSnapshot { get; set; }
    [JsonIgnore] public string? AssessmentSnapshot { get; set; }
    [JsonIgnore] public string? PlanSnapshot { get; set; }
}
