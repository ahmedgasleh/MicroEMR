using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MicroEMR.Application.PatientClinicalHistory;

namespace MicroEMR.Application.PatientImmunizations;

public sealed class PatientImmunizationResponse
{
    public ImmunizationDose? Dose { get; init; }
    public Guid ImmunizationUid { get; init; }
    public Guid PatientUid { get; init; }
    public required string VaccineName { get; init; }
    public DateOnly? AdministrationDate { get; init; }
    public ImmunizationClinicalFields? ClinicalFields { get; init; }
    public ClinicalPartialDate? EffectiveAdministrationDate => ClinicalFields?.AdministrationDate ?? (AdministrationDate is {} date ? new(){Year=date.Year,Month=date.Month,Day=date.Day} : null);
    public string AdministrationDisplay => EffectiveAdministrationDate?.Display ?? "Not recorded";
    public string RefusalDisplay => ClinicalFields?.RefusalDate?.Display ?? "Not recorded";
    public int? DoseNumber { get; init; }
    public string? Route { get; init; }
    public string? Site { get; init; }
    public string? LotNumber { get; init; }
    public string? SourceType { get; init; }
    public string? SourceDescription { get; init; }
    public string? AdministeredByName { get; init; }
    public Guid? EncounterUid { get; init; }
    public string? Notes { get; init; }
    public required string Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long CreatedBy { get; init; }
    public string? CreatedByDisplayName { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public long? UpdatedBy { get; init; }
    public string? UpdatedByDisplayName { get; init; }
    public DateTime? EnteredInErrorAtUtc { get; init; }
    public long? EnteredInErrorBy { get; init; }
    public string? EnteredInErrorByDisplayName { get; init; }
    public string? EnteredInErrorReason { get; init; }
    public required string RowVersion { get; init; }
}

public class SavePatientImmunizationRequest : IValidatableObject
{
    public ImmunizationDose? Dose { get; set; }
    [Required, StringLength(200)] public string VaccineName { get; set; } = string.Empty;
    private DateOnly? administrationDate;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? AdministrationDate { get => administrationDate; set { administrationDate=value; AdministrationDateSpecified=true; } }
    [JsonIgnore] public bool AdministrationDateSpecified { get; private set; }
    [JsonIgnore] public string? RequestCorrelationId { get; set; }
    public ImmunizationClinicalFields? ClinicalFields { get; set; }
    [Range(1, int.MaxValue)] public int? DoseNumber { get; set; }
    [StringLength(100)] public string? Route { get; set; }
    [StringLength(100)] public string? Site { get; set; }
    [StringLength(100)] public string? LotNumber { get; set; }
    [StringLength(30)] public string? SourceType { get; set; }
    [StringLength(500)] public string? SourceDescription { get; set; }
    [StringLength(200)] public string? AdministeredByName { get; set; }
    public Guid? EncounterUid { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Dose is not null)
        {
            foreach (var result in Dose.Validate()) yield return result;
            if (ClinicalFields?.Refused == true && Dose.Amount is not null)
                yield return new("A refusal cannot assert an administered dose.");
        }
        if (string.IsNullOrWhiteSpace(VaccineName))
            yield return new("Vaccine name is required.", [nameof(VaccineName)]);
        if (ClinicalFields is not null)
        {
            foreach (var result in ClinicalFields.Validate()) yield return result;
            if (AdministrationDateSpecified) yield return new("Supply clinical fields or the legacy administration date, not both.");
        }
        else if (this is CreatePatientImmunizationRequest && AdministrationDate is null)
            yield return new("Administration date is required for the legacy administration contract.", [nameof(AdministrationDate)]);
        if (AdministrationDate > DateOnly.FromDateTime(DateTime.UtcNow))
            yield return new("Administration date cannot be in the future.", [nameof(AdministrationDate)]);
        if (ClinicalFields?.Refused == true)
        {
            if (SourceType is not null || AdministeredByName is not null || DoseNumber is not null || Route is not null || Site is not null || LotNumber is not null)
                yield return new("A refusal cannot assert administration source, administrator, ordinal dose, route, site or lot.");
        }
        else
        {
            if (SourceType is not ("ClinicAdministered" or "HistoricalExternal"))
                yield return new("Source type is invalid.", [nameof(SourceType)]);
            if (SourceType == "ClinicAdministered" && string.IsNullOrWhiteSpace(AdministeredByName))
                yield return new("Administered by is required for a clinic-administered immunization.", [nameof(AdministeredByName)]);
        }
    }
}

public sealed class CreatePatientImmunizationRequest : SavePatientImmunizationRequest;

public sealed class UpdatePatientImmunizationRequest : SavePatientImmunizationRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class MarkImmunizationEnteredInErrorRequest
{
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
    [JsonIgnore] public string? RequestCorrelationId { get; set; }
}

public sealed class PatientImmunizationConcurrencyException : Exception;
public sealed class PatientImmunizationTerminalException : Exception;

public interface IPatientImmunizationRepository
{
    Task<IReadOnlyList<PatientImmunizationResponse>> ListAsync(Guid patientUid, string status, CancellationToken token = default);
    Task<PatientImmunizationResponse?> GetAsync(Guid patientUid, Guid immunizationUid, CancellationToken token = default);
    Task<PatientImmunizationResponse> CreateAsync(Guid patientUid, CreatePatientImmunizationRequest request, long actor, CancellationToken token = default);
    Task<PatientImmunizationResponse?> UpdateAsync(Guid patientUid, Guid immunizationUid, UpdatePatientImmunizationRequest request, long actor, CancellationToken token = default);
    Task<PatientImmunizationResponse?> MarkEnteredInErrorAsync(Guid patientUid, Guid immunizationUid, MarkImmunizationEnteredInErrorRequest request, long actor, CancellationToken token = default);
}

public interface IPatientImmunizationService : IPatientImmunizationRepository;

public sealed class PatientImmunizationService(IPatientImmunizationRepository repository) : IPatientImmunizationService
{
    public Task<IReadOnlyList<PatientImmunizationResponse>> ListAsync(Guid patientUid, string status, CancellationToken token = default) =>
        repository.ListAsync(patientUid, status is "Completed" or "Refused" or "EnteredInError" ? status : "All", token);
    public Task<PatientImmunizationResponse?> GetAsync(Guid patientUid, Guid immunizationUid, CancellationToken token = default) => repository.GetAsync(patientUid, immunizationUid, token);
    public Task<PatientImmunizationResponse> CreateAsync(Guid patientUid, CreatePatientImmunizationRequest request, long actor, CancellationToken token = default) { Validate(request); return repository.CreateAsync(patientUid, request, actor, token); }
    public Task<PatientImmunizationResponse?> UpdateAsync(Guid patientUid, Guid immunizationUid, UpdatePatientImmunizationRequest request, long actor, CancellationToken token = default) { Validate(request); return repository.UpdateAsync(patientUid, immunizationUid, request, actor, token); }
    public Task<PatientImmunizationResponse?> MarkEnteredInErrorAsync(Guid patientUid, Guid immunizationUid, MarkImmunizationEnteredInErrorRequest request, long actor, CancellationToken token = default) { Validate(request); return repository.MarkEnteredInErrorAsync(patientUid, immunizationUid, request, actor, token); }
    private static void Validate(object request)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw new PatientImmunizationValidationException(errors[0].ErrorMessage ?? "Invalid immunization.");
    }

}
