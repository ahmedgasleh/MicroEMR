using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.Scheduling;

public sealed class CancelAppointmentViewModel
{
    public Guid AppointmentUid { get; set; }

    [StringLength(500)]
    public string? CancelReason { get; set; }
}
