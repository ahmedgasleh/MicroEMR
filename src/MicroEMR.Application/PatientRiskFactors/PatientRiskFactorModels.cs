using System.ComponentModel.DataAnnotations;
using MicroEMR.Core.Domain;

namespace MicroEMR.Application.PatientRiskFactors;

public class SavePatientRiskFactorRequest : IValidatableObject
{
    [Required, StringLength(200)] public string RiskFactor { get; set; } = "";
    [StringLength(1000)] public string? ExposureDetails { get; set; }
    [StringLength(1)] public string? LifeStageCode { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!FamilyHistoryLifeStages.IsValid(string.IsNullOrWhiteSpace(LifeStageCode) ? null : LifeStageCode))
            yield return new("Select a recorded life stage or leave it blank.", [nameof(LifeStageCode)]);
    }
}
public sealed class CreatePatientRiskFactorRequest : SavePatientRiskFactorRequest;
public sealed class UpdatePatientRiskFactorRequest : SavePatientRiskFactorRequest
{
    [Required] public string RowVersion { get; set; } = "";
}
public sealed class ArchivePatientRiskFactorRequest
{
    [Required] public string RowVersion { get; set; } = "";
}
public sealed record PatientRiskFactorResponse : PatientRiskFactor
{
    public string LifeStage => FamilyHistoryLifeStages.Display(LifeStageCode);
    public string? CreatedByDisplayName { get; init; }
    public string? UpdatedByDisplayName { get; init; }
}
public sealed class PatientRiskFactorNotFoundException : Exception;
public sealed class PatientRiskFactorConcurrencyException : Exception;
public sealed class PatientRiskFactorArchivedException : Exception;
