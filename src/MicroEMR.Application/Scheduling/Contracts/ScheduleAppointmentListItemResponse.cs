namespace MicroEMR.Application.Scheduling.Contracts;

public sealed class ScheduleAppointmentListItemResponse
{
    public Guid AppointmentUid { get; set; }

    public string? PatientDisplayName { get; set; }

    public string? Reason { get; set; }

    public string? AppointmentType { get; set; }

    public DateTime StartDateTimeUtc { get; set; }

    public DateTime EndDateTimeUtc { get; set; }

    public Guid PrimaryResourceUid { get; set; }
}
