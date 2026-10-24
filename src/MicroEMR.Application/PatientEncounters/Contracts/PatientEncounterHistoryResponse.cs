namespace MicroEMR.Application.PatientEncounters.Contracts;

public sealed class PatientEncounterHistoryResponse
{
    public Guid EncounterHistoryUid { get; set; }
    public Guid EncounterUid { get; set; }
    public Guid PatientUid { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? ActionDescription { get; set; }
    public string? DisplayDescription => ActionType switch
    {
        "CppMedicationCreate" => "Medication Summary entry recorded from this encounter.",
        "CppMedicationUpdate" => "Medication Summary entry updated from this encounter.",
        "CppMedicationDiscontinue" => "Medication Summary entry discontinued from this encounter; history retained.",
        _ => ActionDescription
    };
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }
}
