namespace MicroEMR.Web.Models.PatientEncounters;

public sealed class UpdateEncounterNoteViewModel
{
    public Guid PatientUid { get; set; }

    public Guid EncounterUid { get; set; }

    public string? Notes { get; set; }
}
