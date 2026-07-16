namespace MicroEMR.Web.Models.Scheduling;

public sealed class StartEncounterFromAppointmentResponse
{
    public Guid EncounterUid { get; set; }

    public Guid PatientUid { get; set; }

    public Guid AppointmentUid { get; set; }

    public DateTime EncounterDate { get; set; }

    public string? EncounterType { get; set; }

    public string? ReasonForVisit { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool WasCreated { get; set; }
}
