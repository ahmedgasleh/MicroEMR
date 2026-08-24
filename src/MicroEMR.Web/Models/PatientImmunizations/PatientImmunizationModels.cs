using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.PatientImmunizations;

public sealed class PatientImmunizationViewModel
{
    public Guid ImmunizationUid { get; set; } public Guid PatientUid { get; set; }
    public string VaccineName { get; set; } = ""; public DateOnly AdministrationDate { get; set; }
    public int? DoseNumber { get; set; } public string? Route { get; set; } public string? Site { get; set; }
    public string? LotNumber { get; set; } public string SourceType { get; set; } = ""; public string? SourceDescription { get; set; }
    public string? AdministeredByName { get; set; } public Guid? EncounterUid { get; set; } public string? Notes { get; set; }
    public string Status { get; set; } = ""; public DateTime CreatedAtUtc { get; set; } public string? CreatedByDisplayName { get; set; }
    public DateTime? UpdatedAtUtc { get; set; } public string? UpdatedByDisplayName { get; set; }
    public DateTime? EnteredInErrorAtUtc { get; set; } public string? EnteredInErrorByDisplayName { get; set; }
    public string? EnteredInErrorReason { get; set; } public string RowVersion { get; set; } = "";
}

public sealed class SavePatientImmunizationViewModel : IValidatableObject
{
    public Guid PatientUid { get; set; } public Guid? ImmunizationUid { get; set; }
    [Required, StringLength(200)] public string VaccineName { get; set; } = "";
    [Required] public DateOnly? AdministrationDate { get; set; }
    [Range(1,int.MaxValue)] public int? DoseNumber { get; set; }
    [StringLength(100)] public string? Route { get; set; } [StringLength(100)] public string? Site { get; set; }
    [StringLength(100)] public string? LotNumber { get; set; }
    [Required, StringLength(30)] public string SourceType { get; set; } = "";
    [StringLength(500)] public string? SourceDescription { get; set; }
    [StringLength(200)] public string? AdministeredByName { get; set; }
    public Guid? EncounterUid { get; set; } [StringLength(1000)] public string? Notes { get; set; }
    public string? RowVersion { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if(SourceType is not ("ClinicAdministered" or "HistoricalExternal"))yield return new("Source type is invalid.",[nameof(SourceType)]);
        if(SourceType=="ClinicAdministered"&&string.IsNullOrWhiteSpace(AdministeredByName))yield return new("Administered by is required.",[nameof(AdministeredByName)]);
        if(AdministrationDate>DateOnly.FromDateTime(DateTime.UtcNow))yield return new("Administration date cannot be in the future.",[nameof(AdministrationDate)]);
    }
}

public sealed class MarkImmunizationEnteredInErrorViewModel
{
    public Guid PatientUid { get; set; } public Guid ImmunizationUid { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = "";
    [Required] public string RowVersion { get; set; } = "";
}
