namespace MicroEMR.Application.PatientEncounters.Contracts;

public sealed class PatientEncounterHistoryResponse
{
    public Guid EncounterHistoryUid { get; set; }
    public Guid EncounterUid { get; set; }
    public Guid PatientUid { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? ActionDescription { get; set; }
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }
}
