using System.ComponentModel.DataAnnotations;
using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Core.Domain;

namespace MicroEMR.Application.PatientImmunizations;

// Omitted/null group preserves new fields on edit. A supplied group replaces the whole group.
public sealed class ImmunizationClinicalFields
{
    private string? din, manufacturer, type;
    public string? DIN { get => din; set => din = Clean(value); }
    public string? Manufacturer { get => manufacturer; set => manufacturer = Clean(value); }
    public string? ImmunizationTypeCode { get => type; set => type = Clean(value); }
    public bool? Refused { get; set; }
    public ClinicalPartialDate? AdministrationDate { get; set; }
    public ClinicalPartialDate? RefusalDate { get; set; }
    public string ImmunizationType => ImmunizationTypes.Display(ImmunizationTypeCode);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public IEnumerable<ValidationResult> Validate()
    {
        if (DIN?.Length > 100) yield return new("DIN must be 100 characters or fewer.");
        if (Manufacturer?.Length > 200) yield return new("Manufacturer must be 200 characters or fewer.");
        if (!ImmunizationTypes.IsValid(ImmunizationTypeCode)) yield return new("Select a recorded CT-020 immunization type or leave it blank.");
        if (AdministrationDate?.IsValid == false || RefusalDate?.IsValid == false)
            yield return new("Dates must be valid years, year-months or full dates.");
        if (AdministrationDate?.IsValid == true && AdministrationDate.Earliest > DateOnly.FromDateTime(DateTime.UtcNow))
            yield return new("Administration date cannot be in the future.");
        if (Refused == true && (AdministrationDate?.Year is not null || DIN is not null || Manufacturer is not null))
            yield return new("A refusal cannot assert an administration date or administered product.");
        if (Refused != true && RefusalDate?.Year is not null)
            yield return new("A refusal date requires an explicit refused outcome.");
    }
}

public sealed class PatientImmunizationValidationException(string message) : Exception(message);
public sealed class PatientImmunizationNotFoundException : Exception;
