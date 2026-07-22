using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientEncounters.Contracts;

public sealed class CreateEncounterAddendumRequest
{
    [Required]
    public string AddendumText { get; set; } = string.Empty;
}
