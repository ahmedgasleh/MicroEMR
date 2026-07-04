using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.PatientMedications;

public sealed class CreatePatientMedicationRequest : IValidatableObject
{
    [Required]
    [StringLength(200)]
    public string MedicationName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Strength { get; set; }

    [StringLength(100)]
    public string? DosageForm { get; set; }

    [StringLength(100)]
    public string? Route { get; set; }

    [StringLength(500)]
    public string? Directions { get; set; }

    [StringLength(100)]
    public string? Frequency { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [StringLength(300)]
    public string? Indication { get; set; }

    [StringLength(200)]
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
