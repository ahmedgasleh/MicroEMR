using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.PatientMedications;

public sealed class CreatePatientMedicationViewModel : IValidatableObject
{
    public Guid PatientUid { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Medication name")]
    public string MedicationName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Strength { get; set; }

    [StringLength(100)]
    [Display(Name = "Dosage form")]
    public string? DosageForm { get; set; }

    [StringLength(100)]
    public string? Route { get; set; }

    [StringLength(500)]
    public string? Directions { get; set; }

    [StringLength(100)]
    public string? Frequency { get; set; }

    [Display(Name = "Start date")]
    public DateTime? StartDate { get; set; }

    [Display(Name = "End date")]
    public DateTime? EndDate { get; set; }

    [StringLength(300)]
    public string? Indication { get; set; }

    [StringLength(200)]
    [Display(Name = "Prescriber")]
    public string? PrescriberName { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (StartDate.HasValue
            && EndDate.HasValue
            && EndDate.Value.Date < StartDate.Value.Date)
        {
            yield return new ValidationResult(
                "End date cannot be before start date.",
                new[] { nameof(EndDate) });
        }
    }
}
