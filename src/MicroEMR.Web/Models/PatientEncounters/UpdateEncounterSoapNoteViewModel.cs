namespace MicroEMR.Web.Models.PatientEncounters;

public sealed class UpdateEncounterSoapNoteViewModel
{
    public Guid PatientUid { get; set; }
    public Guid EncounterUid { get; set; }
    public string? SubjectiveNote { get; set; }
    public string? ObjectiveNote { get; set; }
    public string? AssessmentNote { get; set; }
    public string? PlanNote { get; set; }
}
