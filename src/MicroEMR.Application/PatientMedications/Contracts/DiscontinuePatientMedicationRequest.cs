using System.ComponentModel.DataAnnotations;
namespace MicroEMR.Application.PatientMedications.Contracts;
public sealed class DiscontinuePatientMedicationRequest
{
    [StringLength(500)] public string? DiscontinueReason { get; set; }
}
