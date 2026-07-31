using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientEncounters.Contracts;

public sealed class SignPatientEncounterRequest
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}
