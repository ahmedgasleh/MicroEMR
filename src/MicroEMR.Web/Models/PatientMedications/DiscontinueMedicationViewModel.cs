using System.ComponentModel.DataAnnotations;
namespace MicroEMR.Web.Models.PatientMedications;
public sealed class DiscontinueMedicationViewModel
{
    public Guid PatientUid { get; set; }
    public Guid MedicationUid { get; set; }
    [StringLength(500)] public string? DiscontinueReason { get; set; }
}
