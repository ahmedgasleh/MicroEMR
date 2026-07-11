using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.Scheduling;

public sealed class UpdateAppointmentViewModel
{
    public Guid AppointmentUid { get; set; }
    public Guid PrimaryResourceUid { get; set; }
    public Guid? RoomResourceUid { get; set; }
    public DateTime StartDateTimeLocal { get; set; }
    public DateTime EndDateTimeLocal { get; set; }

    [StringLength(100)]
    public string? AppointmentType { get; set; }
    [StringLength(500)]
    public string? Reason { get; set; }
    [StringLength(1000)]
    public string? Notes { get; set; }
}
