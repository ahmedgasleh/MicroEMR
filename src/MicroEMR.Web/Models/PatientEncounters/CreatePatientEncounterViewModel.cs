using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.PatientEncounters;

public sealed class CreatePatientEncounterViewModel
{
    public Guid PatientUid { get; set; }

    [Required]
    [Display(Name = "Encounter date/time")]
    public DateTime EncounterDateLocal { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Encounter type")]
    public string EncounterType { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Reason for visit")]
    public string? ReasonForVisit { get; set; }

    [StringLength(200)]
    [Display(Name = "Location")]
    public string? LocationName { get; set; }

    [StringLength(200)]
    [Display(Name = "Provider")]
    public string? ProviderName { get; set; }
}
