namespace MicroEMR.Web.Models.Scheduling;

public sealed class CreateScheduleAppointmentRequest
{
    public Guid PatientUid { get; set; }
    public Guid PrimaryResourceUid { get; set; }
    public Guid? RoomResourceUid { get; set; }
    public DateTime StartDateTimeUtc { get; set; }
    public DateTime EndDateTimeUtc { get; set; }
    public string? AppointmentType { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}
