using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;
using MicroEMR.Core.Domain;

namespace MicroEMR.Application.PatientClinicalHistory;

// A missing ClinicalFields object on an update preserves the recorded fields.
// A supplied object replaces this group; {} explicitly clears it.
public sealed class HistoryClinicalFields
{
    public ClinicalPartialDate? OnsetDate { get; set; }
    public ClinicalPartialDate? ResolutionDate { get; set; }
    public ClinicalPartialDate? ProcedureDate { get; set; }
    private string? lifeStageCode;
    public string? LifeStageCode { get => lifeStageCode; set => lifeStageCode = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
    private string? clinicalStatus;
    public string? ClinicalStatus { get => clinicalStatus; set => clinicalStatus = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
    private string? notes;
    public string? Notes { get => notes; set => notes = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
    public string LifeStage => FamilyHistoryLifeStages.Display(LifeStageCode);

    public IEnumerable<ValidationResult> Validate(string kind)
    {
        foreach (var (name, date) in new[] { (nameof(OnsetDate), OnsetDate), (nameof(ResolutionDate), ResolutionDate), (nameof(ProcedureDate), ProcedureDate) })
        {
            if (date is null) continue;
            if (!date.IsValid) yield return new($"{name} must be a valid year, year-month or full date.", [name]);
            else if (date.Earliest is { } earliest && earliest > DateOnly.FromDateTime(DateTime.UtcNow))
                yield return new($"{name} cannot be in the future.", [name]);
        }
        if (!FamilyHistoryLifeStages.IsValid(LifeStageCode)) yield return new("Select a recorded life stage or leave it blank.", [nameof(LifeStageCode)]);
        if (ClinicalStatus is not (null or "Active" or "Resolved")) yield return new("Clinical status must be Active, Resolved or not recorded.", [nameof(ClinicalStatus)]);
        if (Notes?.Length > 1000) yield return new("Notes must be 1000 characters or fewer.", [nameof(Notes)]);
        if (kind == "Medical" && ProcedureDate?.Year is not null) yield return new("Procedure date applies to Surgical or Procedure history.", [nameof(ProcedureDate)]);
        if (kind is "Surgical" or "Procedure" && (OnsetDate?.Year is not null || ResolutionDate?.Year is not null || LifeStageCode is not null || ClinicalStatus is not null))
            yield return new("Onset, resolution, life stage and clinical status apply to Medical history.");
        if (ClinicalStatus == "Active" && ResolutionDate?.Year is not null) yield return new("An explicitly Active condition cannot have a clinical resolution date.", [nameof(ResolutionDate)]);
        if (OnsetDate?.IsValid == true && ResolutionDate?.IsValid == true && ResolutionDate.Latest is { } end && OnsetDate.Earliest is { } start && end < start)
            yield return new("Resolution cannot be before onset.", [nameof(ResolutionDate)]);
    }
}

public sealed class ClinicalPartialDate
{
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
    [JsonIgnore] public bool IsValid => Year is null ? Month is null && Day is null
        : Year is >= 1 and <= 9999 && (Month is null ? Day is null
        : Month is >= 1 and <= 12 && (Day is null || Day >= 1 && Day <= DateTime.DaysInMonth(Year.Value, Month.Value)));
    [JsonIgnore] public DateOnly? Earliest => IsValid && Year.HasValue ? new(Year.Value, Month ?? 1, Day ?? 1) : null;
    [JsonIgnore] public DateOnly? Latest => IsValid && Year.HasValue ? new(Year.Value, Month ?? 12, Day ?? DateTime.DaysInMonth(Year.Value, Month ?? 12)) : null;
    public string Display => !IsValid || Year is null ? "Not recorded" : Year.Value.ToString("D4", CultureInfo.InvariantCulture)
        + (Month is null ? "" : "-" + Month.Value.ToString("D2", CultureInfo.InvariantCulture))
        + (Day is null ? "" : "-" + Day.Value.ToString("D2", CultureInfo.InvariantCulture));
}
