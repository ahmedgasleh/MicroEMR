using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientVitals.Contracts;

public sealed class UpdatePatientVitalRequest : CreatePatientVitalRequest
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}
