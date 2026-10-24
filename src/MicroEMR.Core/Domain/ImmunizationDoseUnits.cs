namespace MicroEMR.Core.Domain;

// Owner-approved local canonical codes; no inferred product units or dose defaults.
public static class ImmunizationDoseUnits
{
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { "mL", "mg", "mcg", "units" });
    public static bool IsValid(string? code) => code is null || All.Contains(code, StringComparer.Ordinal);
}
