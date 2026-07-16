namespace MicroEMR.Web.Models.PatientMedications;
public sealed class UpdatePatientMedicationRequest
{
    public string MedicationName { get; set; } = string.Empty; public string? Strength { get; set; }
    public string? DosageForm { get; set; } public string? Route { get; set; } public string? Directions { get; set; }
    public string? Frequency { get; set; } public DateTime? StartDate { get; set; } public DateTime? EndDate { get; set; }
    public string? Indication { get; set; } public string? PrescriberName { get; set; }
    public string Status { get; set; } = "Active"; public string? Notes { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
