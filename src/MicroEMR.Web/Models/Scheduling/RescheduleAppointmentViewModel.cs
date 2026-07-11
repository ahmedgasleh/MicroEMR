namespace MicroEMR.Web.Models.Scheduling;

public sealed class RescheduleAppointmentViewModel
{
    public Guid AppointmentUid { get; set; }
    public Guid PrimaryResourceUid { get; set; }
    public Guid? RoomResourceUid { get; set; }
    public DateTime StartDateTimeLocal { get; set; }
    public DateTime EndDateTimeLocal { get; set; }
}
