using MicroEMR.Application.PatientImmunizations;
using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.PatientImmunizations;

public sealed class PatientImmunizationViewModel
{
    public Guid ImmunizationUid { get; set; } public Guid PatientUid { get; set; }
    public string VaccineName { get; set; } = ""; public DateOnly? AdministrationDate { get; set; }
    public ImmunizationClinicalFields? ClinicalFields { get; set; }
    public int? DoseNumber { get; set; } public string? Route { get; set; } public string? Site { get; set; }
    public string? LotNumber { get; set; } public string? SourceType { get; set; } public string? SourceDescription { get; set; }
    public string? AdministeredByName { get; set; } public Guid? EncounterUid { get; set; } public string? Notes { get; set; }
    public string Status { get; set; } = ""; public DateTime CreatedAtUtc { get; set; } public string? CreatedByDisplayName { get; set; }
    public DateTime? UpdatedAtUtc { get; set; } public string? UpdatedByDisplayName { get; set; }
    public DateTime? EnteredInErrorAtUtc { get; set; } public string? EnteredInErrorByDisplayName { get; set; }
    public string? EnteredInErrorReason { get; set; } public string RowVersion { get; set; } = "";
}

public sealed class SavePatientImmunizationViewModel : SavePatientImmunizationRequest
{
    public Guid PatientUid { get; set; }
    public Guid? ImmunizationUid { get; set; }
    public string? RowVersion { get; set; }
    public bool ClinicalFieldsIncluded { get; set; }
}

public sealed class MarkImmunizationEnteredInErrorViewModel
{
    public Guid PatientUid { get; set; } public Guid ImmunizationUid { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = "";
    [Required] public string RowVersion { get; set; } = "";
}
