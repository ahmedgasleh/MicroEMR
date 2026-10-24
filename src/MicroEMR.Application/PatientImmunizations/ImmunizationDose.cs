using System.ComponentModel.DataAnnotations;
using System.Globalization;
using MicroEMR.Core.Domain;

namespace MicroEMR.Application.PatientImmunizations;

// Omitted/null group preserves dose on update; a supplied empty group explicitly clears it.
public sealed class ImmunizationDose
{
    public decimal? Amount { get; set; }
    public string? UnitCode { get; set; }
    public string Display => Amount is {} amount && UnitCode is not null
        ? amount.ToString("0.###", CultureInfo.InvariantCulture) + " " + UnitCode : "Not recorded";

    public IEnumerable<ValidationResult> Validate()
    {
        if (Amount.HasValue != (UnitCode is not null))
            yield return new("Dose amount and unit must both be supplied or both left blank.");
        if (Amount is {} amount && (amount <= 0 || amount > 9999.999m || ((decimal.GetBits(amount)[3] >> 16) & 255) > 3))
            yield return new("Dose amount must be greater than 0, at most 9999.999, with at most 3 fractional digits.");
        if (!ImmunizationDoseUnits.IsValid(UnitCode))
            yield return new("Select a supported dose unit: mL, mg, mcg or units.");
    }
}
