using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientEncounters.Contracts;

public sealed class CreateEncounterAddendumRequest
{
    [Required]
    public string AddendumText { get; set; } = string.Empty;

    [Required]
    public string ReasonForAmendment { get; set; } = string.Empty;
}
