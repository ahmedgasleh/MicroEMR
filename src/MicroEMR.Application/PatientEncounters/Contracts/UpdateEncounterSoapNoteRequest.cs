namespace MicroEMR.Application.PatientEncounters.Contracts;

public sealed class UpdateEncounterSoapNoteRequest
{
    public string? SubjectiveNote { get; set; }
    public string? ObjectiveNote { get; set; }
    public string? AssessmentNote { get; set; }
    public string? PlanNote { get; set; }
}
