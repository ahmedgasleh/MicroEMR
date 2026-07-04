using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Api.Models.PatientEncounters;

public sealed class CreatePatientEncounterRequest
{
    [Required]
    public DateTime EncounterDateUtc { get; set; }

    [Required]
    [StringLength(100)]
    public string EncounterType { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ReasonForVisit { get; set; }

    [StringLength(200)]
    public string? LocationName { get; set; }

    [StringLength(200)]
    public string? ProviderName { get; set; }
}
