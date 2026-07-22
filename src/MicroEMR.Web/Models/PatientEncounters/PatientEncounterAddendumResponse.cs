namespace MicroEMR.Web.Models.PatientEncounters;

public sealed class PatientEncounterAddendumResponse
{
    public Guid EncounterAddendumUid { get; set; }
    public Guid EncounterUid { get; set; }
    public Guid PatientUid { get; set; }
    public string AddendumText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }
}
