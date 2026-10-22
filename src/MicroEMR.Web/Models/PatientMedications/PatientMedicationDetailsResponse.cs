namespace MicroEMR.Web.Models.PatientMedications;

public sealed class PatientMedicationDetailsResponse
{
    public MicroEMR.Application.PatientMedications.Contracts.MedicationComplianceRevision? ComplianceAssessment { get; set; }
    public IReadOnlyList<MicroEMR.Application.PatientMedications.Contracts.MedicationComplianceRevision> ComplianceHistory { get; set; } = [];
    public bool? LongTermIntent { get; set; }
    public bool PastMedication => Status == "Discontinued";
    public string? Classification { get; set; }

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

    public string RowVersion { get; set; } = string.Empty;
}
