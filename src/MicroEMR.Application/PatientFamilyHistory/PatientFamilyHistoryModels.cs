using System.ComponentModel.DataAnnotations;
using MicroEMR.Core.Domain;

namespace MicroEMR.Application.PatientFamilyHistory;

public class SavePatientFamilyHistoryRequest : IValidatableObject
{
    [Required, StringLength(100)] public string Relationship { get; set; } = "";
    [Required, StringLength(1000)] public string ConditionDescription { get; set; } = "";
    [StringLength(100)] public string? AgeAtOnset { get; set; }
    [StringLength(1)] public string? LifeStageCode { get; set; }
    [StringLength(1000)] public string? Treatment { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!FamilyHistoryLifeStages.IsValid(string.IsNullOrWhiteSpace(LifeStageCode) ? null : LifeStageCode))
            yield return new("Select a recorded life stage or leave it blank.", [nameof(LifeStageCode)]);
    }
}
public sealed class CreatePatientFamilyHistoryRequest : SavePatientFamilyHistoryRequest;
public sealed class UpdatePatientFamilyHistoryRequest : SavePatientFamilyHistoryRequest
{
    [Required] public string RowVersion { get; set; } = "";
}
public sealed class ArchivePatientFamilyHistoryRequest
{
    [Required] public string RowVersion { get; set; } = "";
}
public sealed record PatientFamilyHistoryResponse
{
    public Guid FamilyHistoryUid { get; init; }
    public Guid PatientUid { get; init; }
    public string Relationship { get; init; } = "";
    public string ConditionDescription { get; init; } = "";
    public string? AgeAtOnset { get; init; }
    public string? LifeStageCode { get; init; }
    public string LifeStage => FamilyHistoryLifeStages.Display(LifeStageCode);
    public string? Treatment { get; init; }
    public string? Notes { get; init; }
    public string Status { get; init; } = "Active";
    public DateTime CreatedAt { get; init; }
    public long CreatedBy { get; init; }
    public string? CreatedByDisplayName { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public long? UpdatedBy { get; init; }
    public string? UpdatedByDisplayName { get; init; }
    public string RowVersion { get; init; } = "";
}
public sealed class PatientFamilyHistoryNotFoundException : Exception;
public sealed class PatientFamilyHistoryConcurrencyException : Exception;
public sealed class PatientFamilyHistoryArchivedException : Exception;
