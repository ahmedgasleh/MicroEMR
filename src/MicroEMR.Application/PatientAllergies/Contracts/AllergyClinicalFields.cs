using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.Json;
using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Core.Domain;

namespace MicroEMR.Application.PatientAllergies.Contracts;

public sealed class AllergyClinicalFields
{
    public const string LifeStageSourceVersion = "CDS-S 2.1 Final / 2026-06-05 / CT-016";
    private string? applicability;
    [JsonIgnore] public string? DrugApplicability { get => applicability; set { applicability=string.IsNullOrWhiteSpace(value)?null:value.Trim(); DrugApplicabilitySpecified=true; } }
    [JsonIgnore] public bool DrugApplicabilitySpecified { get; private set; }
    [JsonPropertyName("DrugApplicability"),JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement DrugApplicabilityInput
    {
        get => DrugApplicabilitySpecified ? JsonSerializer.SerializeToElement(DrugApplicability) : default;
        set { if(value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))throw new JsonException("Drug applicability must be text or null."); DrugApplicability=value.ValueKind==JsonValueKind.Null?null:value.GetString(); }
    }
    private string? din, stage;
    public string? DrugDIN { get => din; set => din=string.IsNullOrWhiteSpace(value)?null:value.Trim(); }
    public string? LifeStageCode { get => stage; set => stage=string.IsNullOrWhiteSpace(value)?null:value.Trim(); }
    public ClinicalPartialDate? OnsetDate { get; set; }
    public string LifeStage => FamilyHistoryLifeStages.Display(LifeStageCode);
}

public abstract class AllergySaveRequest : IValidatableObject
{
    [Required,StringLength(200)] public string AllergenName { get; set; } = "";
    [StringLength(100)] public string? AllergenType { get; set; }
    [StringLength(500)] public string? Reaction { get; set; }
    [StringLength(30)] public string? Severity { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    private DateTime? onset;
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? OnsetDate { get => onset; set { onset=value; OnsetDateSpecified=true; } }
    [JsonIgnore] public bool OnsetDateSpecified { get; private set; }
    [JsonIgnore] public string? RequestCorrelationId { get; set; }
    public AllergyClinicalFields? ClinicalFields { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if(string.IsNullOrWhiteSpace(AllergenName)) yield return new("Allergen name is required.");
        if(ClinicalFields is {} fields)
        {
            if(OnsetDateSpecified) yield return new("Supply clinical fields or the legacy onset, not both.");
            if(fields.OnsetDate?.IsValid==false) yield return new("Onset must be a valid year, year-month or full date.");
            if(!FamilyHistoryLifeStages.IsValid(fields.LifeStageCode)) yield return new("Select a recorded CT-016 life stage or leave it blank.");
            if(fields.DrugDIN?.Length>100) yield return new("Drug DIN must be 100 characters or fewer.");
            if(fields.DrugApplicability is not null && fields.DrugApplicability is not ("Drug" or "Non-drug" or "Unknown")) yield return new("Select Drug, Non-drug or Unknown applicability.");
            // Omitted applicability on updates is checked against the owned stored record by SQL.
            if(fields.DrugDIN is not null && (fields.DrugApplicability is not null ? fields.DrugApplicability != "Drug" : this is CreatePatientAllergyRequest && AllergenType?.Trim() != "Medication")) yield return new("DIN entry requires explicit Drug applicability or the existing Medication classification. Reconcile a recorded DIN deliberately.");
        }
    }
}

public sealed record AllergySnapshot(IReadOnlyList<PatientAllergyListItemResponse> Items,AllergyDocumentationStateResponse Documentation);
public sealed class PatientAllergyValidationException(string message) : Exception(message);
public sealed class PatientAllergyNotFoundException : Exception;
