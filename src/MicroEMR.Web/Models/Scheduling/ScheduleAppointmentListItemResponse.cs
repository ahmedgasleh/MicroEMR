namespace MicroEMR.Web.Models.Scheduling;

public sealed class ScheduleAppointmentListItemResponse
{
    public Guid AppointmentUid { get; set; }

    public string PatientDisplayName { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string? AppointmentType { get; set; }

    public DateTime StartDateTimeUtc { get; set; }

    public DateTime EndDateTimeUtc { get; set; }

    public Guid PrimaryResourceUid { get; set; }
}
