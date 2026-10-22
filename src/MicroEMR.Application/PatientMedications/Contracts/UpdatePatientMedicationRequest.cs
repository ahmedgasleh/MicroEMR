using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientMedications.Contracts;

public sealed class UpdatePatientMedicationRequest : IValidatableObject
{
    public MicroEMR.Application.PatientMedications.Contracts.MedicationPlanFields? PlanFields { get; set; }
    public MicroEMR.Application.PatientMedications.Contracts.MedicationComplianceAssessment? ComplianceAssessment { get; set; }
    public string? Classification { get; set; }

    [Required, StringLength(200)] public string MedicationName { get; set; } = string.Empty;
    [StringLength(100)] public string? Strength { get; set; }
    [StringLength(100)] public string? DosageForm { get; set; }
    [StringLength(100)] public string? Route { get; set; }
    [StringLength(500)] public string? Directions { get; set; }
    [StringLength(100)] public string? Frequency { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    [StringLength(300)] public string? Indication { get; set; }
    [StringLength(200)] public string? PrescriberName { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = "Active";
    [StringLength(1000)] public string? Notes { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ComplianceAssessment is {} assessment) foreach (var error in assessment.Validate(new(assessment))) yield return error;
        if (!MicroEMR.Core.PatientMedications.MedicationClassifications.IsValid(Classification))
            yield return new ValidationResult("Select Ongoing, Acute or Unclassified.", new[] { nameof(Classification) });

        if (StartDate.HasValue && EndDate.HasValue && EndDate.Value.Date < StartDate.Value.Date)
            yield return new ValidationResult("End date cannot be before start date.", [nameof(EndDate)]);
    }
}
