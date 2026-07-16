using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.PatientAllergies;

public sealed class EditPatientAllergyViewModel
{
    public Guid PatientUid { get; set; }
    public Guid AllergyUid { get; set; }
    [Required, StringLength(200), Display(Name = "Allergen name")]
    public string AllergenName { get; set; } = string.Empty;
    [StringLength(100), Display(Name = "Allergen type")] public string? AllergenType { get; set; }
    [StringLength(500)] public string? Reaction { get; set; }
    [StringLength(30)] public string? Severity { get; set; }
    [Display(Name = "Onset date")] public DateTime? OnsetDate { get; set; }
    [Required, StringLength(30)] public string Status { get; set; } = "Active";
    [StringLength(1000)] public string? Notes { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}
