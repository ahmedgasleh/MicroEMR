namespace MicroEMR.Web.Models.PatientMedications;

public sealed class PatientMedicationListItemResponse
{
    public Guid MedicationUid { get; set; }

    public Guid PatientUid { get; set; }

    public string MedicationName { get; set; } = string.Empty;

    public string? Strength { get; set; }

    public string? DosageForm { get; set; }

    public string? Route { get; set; }

    public string? Directions { get; set; }

    public string? Frequency { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Indication { get; set; }

    public string? PrescriberName { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = string.Empty;

    public long? CreatedBy { get; set; }

    public string? CreatedByDisplayName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
