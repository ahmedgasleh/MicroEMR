namespace MicroEMR.Application.PatientEncounters.Contracts;

public sealed class PatientEncounterAddendumResponse
{
    public Guid EncounterAddendumUid { get; set; }
    public Guid EncounterUid { get; set; }
    public Guid PatientUid { get; set; }
    public string AddendumText { get; set; } = string.Empty;
    public string ReasonForAmendment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public long? SignedBy { get; set; }
    public string? SignedByDisplayName { get; set; }
    public DateTime SignedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
