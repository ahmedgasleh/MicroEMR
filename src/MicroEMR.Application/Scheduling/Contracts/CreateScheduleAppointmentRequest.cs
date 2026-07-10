using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.Scheduling.Contracts;

public sealed class CreateScheduleAppointmentRequest
{
    public Guid PatientUid { get; set; }
    public Guid PrimaryResourceUid { get; set; }
    public Guid? RoomResourceUid { get; set; }
    public DateTime StartDateTimeUtc { get; set; }
    public DateTime EndDateTimeUtc { get; set; }

    [StringLength(100)]
    public string? AppointmentType { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
