using System.ComponentModel.DataAnnotations;
namespace MicroEMR.Web.Models.PatientMedications;
public sealed class DiscontinuePatientMedicationRequest
{
    [StringLength(500)] public string? DiscontinueReason { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}
