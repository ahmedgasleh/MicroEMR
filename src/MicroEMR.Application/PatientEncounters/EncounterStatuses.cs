namespace MicroEMR.Application.PatientEncounters;

public static class EncounterStatuses
{
    // The persisted legacy value remains Open for migration compatibility;
    // it is the single supported pre-sign (Draft) state.
    public const string Draft = "Open";
    public const string Signed = "Signed";

    public static bool IsEditable(string? status) =>
        string.Equals(status, Draft, StringComparison.OrdinalIgnoreCase);
}
