namespace MicroEMR.Web.Models.Scheduling;

public sealed class ScheduleAppointmentListItemResponse
{
    public Guid AppointmentUid { get; set; }

    public Guid PatientUid { get; set; }

    public string PatientDisplayName { get; set; } = string.Empty;

    public string? PatientHealthCardNumber { get; set; }
    public DateOnly? PatientDateOfBirth { get; set; }
    public string? PatientGender { get; set; }

    public string? ChartNumber { get; set; }

    public string? Reason { get; set; }

    public string? AppointmentType { get; set; }

    public bool IsCritical { get; set; }

    public DateTime StartDateTimeUtc { get; set; }

    public DateTime EndDateTimeUtc { get; set; }

    public Guid PrimaryResourceUid { get; set; }

    public string? PrimaryResourceName { get; set; }

    public string Status { get; set; } = string.Empty;
    public Guid? LinkedEncounterUid { get; set; }
    public string? LinkedEncounterStatus { get; set; }
}
