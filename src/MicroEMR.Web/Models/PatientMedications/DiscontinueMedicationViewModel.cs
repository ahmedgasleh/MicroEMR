using System.ComponentModel.DataAnnotations;
namespace MicroEMR.Web.Models.PatientMedications;
public sealed class DiscontinueMedicationViewModel
{
    public Guid PatientUid { get; set; }
    public Guid MedicationUid { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(500)] public string? DiscontinueReason { get; set; }
}
