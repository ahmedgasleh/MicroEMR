namespace MicroEMR.Application.Scheduling.Contracts;

public sealed class UpdateAppointmentStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
