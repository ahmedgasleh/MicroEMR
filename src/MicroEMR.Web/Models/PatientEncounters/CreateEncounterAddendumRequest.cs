namespace MicroEMR.Web.Models.PatientEncounters;

public sealed class CreateEncounterAddendumRequest
{
    public string AddendumText { get; set; } = string.Empty;
    public string ReasonForAmendment { get; set; } = string.Empty;
}
