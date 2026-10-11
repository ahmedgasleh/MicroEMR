namespace MicroEMR.Application.PatientEncounters.Contracts;

public sealed class UpdateEncounterStructuredDataRequest
{
    public string StructuredDataJson { get; set; } = string.Empty;
    public string[]? ChangedFieldKeys { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
