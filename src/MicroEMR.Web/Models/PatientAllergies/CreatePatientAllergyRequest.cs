using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.PatientAllergies;

public sealed class CreatePatientAllergyRequest
{
    [Required]
    [StringLength(200)]
    public string AllergenName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? AllergenType { get; set; }

    [StringLength(500)]
    public string? Reaction { get; set; }

    [StringLength(30)]
    public string? Severity { get; set; }

    public DateTime? OnsetDate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
