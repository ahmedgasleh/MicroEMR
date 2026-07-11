using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.Scheduling.Contracts;

public sealed class CancelScheduleAppointmentRequest
{
    [StringLength(500)]
    public string? CancelReason { get; set; }
}
