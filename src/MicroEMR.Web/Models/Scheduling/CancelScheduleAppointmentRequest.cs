using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.Scheduling;

public sealed class CancelScheduleAppointmentRequest
{
    [StringLength(500)]
    public string? CancelReason { get; set; }
}
