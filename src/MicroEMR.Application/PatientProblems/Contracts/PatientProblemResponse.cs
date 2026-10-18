using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MicroEMR.Application.PatientClinicalHistory;

namespace MicroEMR.Application.PatientProblems.Contracts;

public sealed class PatientProblemResponse
{
    public Guid PatientProblemUid { get; set; }
    public Guid PatientUid { get; set; }
    public string ProblemName { get; set; } = string.Empty;
    public string? ProblemDescription { get; set; }
    public DateTime? OnsetDate { get; set; }
    [JsonIgnore] public ClinicalPartialDate? EffectiveOnset => ClinicalFields?.OnsetDate ?? (OnsetDate is {} exact ? new(){Year=exact.Year,Month=exact.Month,Day=exact.Day} : null);
    public ProblemClinicalFields? ClinicalFields { get; set; }
    public ClinicalPartialDate? ClinicalResolutionDate { get; set; }
    public string ProblemStatus { get; set; } = string.Empty;
    public DateTime? ResolvedAt { get; set; }
    public long? ResolvedBy { get; set; }
    public string? ResolvedByDisplayName { get; set; }
    public string? ResolutionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public abstract class ProblemSaveRequest : IValidatableObject
{
    [Required, StringLength(200)] public string ProblemName { get; set; } = string.Empty;
    [StringLength(1000)] public string? ProblemDescription { get; set; }
    private DateTime? onsetDate;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? OnsetDate { get => onsetDate; set { onsetDate = value; OnsetDateSpecified = true; } }
    [JsonIgnore] public bool OnsetDateSpecified { get; private set; }
    public ProblemClinicalFields? ClinicalFields { get; set; }
    [JsonIgnore] public string? RequestCorrelationId { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ClinicalFields is not null && OnsetDateSpecified)
            yield return new("Supply either clinical fields or the legacy onset date, not both.");
        foreach (var error in ClinicalFields?.Validate() ?? []) yield return error;
    }
}
public class CreatePatientProblemRequest : ProblemSaveRequest;
public class UpdatePatientProblemRequest : ProblemSaveRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
}
public class ResolvePatientProblemRequest : IValidatableObject
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(500)] public string? ResolutionReason { get; set; }
    public ClinicalPartialDate? ClinicalResolutionDate { get; set; }
    [JsonIgnore] public string? RequestCorrelationId { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ClinicalResolutionDate?.IsValid == false) yield return new("Clinical resolution must be a valid year, year-month or full date.", [nameof(ClinicalResolutionDate)]);
    }
}
