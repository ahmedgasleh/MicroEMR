using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MicroEMR.Application.PatientClinicalHistory;

namespace MicroEMR.Application.PatientPrescriptions;

// A missing group on update preserves the recorded dates. A supplied group replaces
// both dates; only the optional start date can be unknown.
public sealed class PrescriptionDateFields
{
    public PrescriptionDateValue? WrittenDate { get; set; }
    public PrescriptionDateValue? StartDate { get; set; }

    public IEnumerable<ValidationResult> Validate()
    {
        if (WrittenDate is null || !WrittenDate.IsValid || WrittenDate.Precision == "Unknown")
            yield return new("Written date requires a valid year, year-month or full date with matching precision.", ["DateFields.WrittenDate"]);
        if (StartDate is not null && !StartDate.IsValid)
            yield return new("Start date must be unknown or a valid year, year-month or full date with matching precision.", ["DateFields.StartDate"]);
    }

    public static PrescriptionDateFields From(DateOnly? written, DateOnly? start) => new()
    { WrittenDate = PrescriptionDateValue.From(written), StartDate = PrescriptionDateValue.From(start) };
}

public sealed class PrescriptionDateValue
{
    public string Precision { get; set; } = "";
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
    [JsonIgnore] public bool IsValid => Parts.IsValid && (Precision switch
    {
        "Unknown" => Year is null && Month is null && Day is null,
        "Year" => Year is not null && Month is null && Day is null,
        "Month" => Year is not null && Month is not null && Day is null,
        "Day" => Year is not null && Month is not null && Day is not null,
        _ => false
    });
    [JsonIgnore] public ClinicalPartialDate Parts => new() { Year = Year, Month = Month, Day = Day };
    [JsonIgnore] public string Display => IsValid ? Parts.Display : "Not recorded";
    public static PrescriptionDateValue From(DateOnly? value) => value is { } date
        ? new() { Precision = "Day", Year = date.Year, Month = date.Month, Day = date.Day }
        : new() { Precision = "Unknown" };
}
