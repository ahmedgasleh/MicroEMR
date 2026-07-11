namespace MicroEMR.Application.Scheduling.Contracts;

public sealed class CancelScheduleAppointmentResponse
{
    public Guid AppointmentUid { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
}
