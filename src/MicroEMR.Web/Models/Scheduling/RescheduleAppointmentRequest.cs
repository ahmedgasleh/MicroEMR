namespace MicroEMR.Web.Models.Scheduling;

public sealed class RescheduleAppointmentRequest
{
    public Guid PrimaryResourceUid { get; set; }
    public Guid? RoomResourceUid { get; set; }
    public DateTime StartDateTimeUtc { get; set; }
    public DateTime EndDateTimeUtc { get; set; }
}
