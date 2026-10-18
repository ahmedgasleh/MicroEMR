using System.ComponentModel.DataAnnotations;
using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Core.Domain;

namespace MicroEMR.Application.PatientProblems.Contracts;

// Omitted/null group preserves fields on edit; supplied group replaces them; {} explicitly clears.
public sealed class ProblemClinicalFields
{
    public ClinicalPartialDate? OnsetDate { get; set; }
    private string? lifeStageCode, notes;
    public string? LifeStageCode { get => lifeStageCode; set => lifeStageCode = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
    public string? Notes { get => notes; set => notes = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
    public string LifeStage => FamilyHistoryLifeStages.Display(LifeStageCode);
    public IEnumerable<ValidationResult> Validate()
    {
        if (OnsetDate?.IsValid == false) yield return new("Onset must be a valid year, year-month or full date.", [nameof(OnsetDate)]);
        if (!FamilyHistoryLifeStages.IsValid(LifeStageCode)) yield return new("Select a recorded life stage or leave it blank.", [nameof(LifeStageCode)]);
        if (Notes?.Length > 1000) yield return new("Notes must be 1000 characters or fewer.", [nameof(Notes)]);
    }
}
