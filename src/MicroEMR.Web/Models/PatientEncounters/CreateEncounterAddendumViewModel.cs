namespace MicroEMR.Web.Models.PatientEncounters;

public sealed class CreateEncounterAddendumViewModel
{
    public Guid PatientUid { get; set; }
    public Guid EncounterUid { get; set; }
    public string? AddendumText { get; set; }
}
