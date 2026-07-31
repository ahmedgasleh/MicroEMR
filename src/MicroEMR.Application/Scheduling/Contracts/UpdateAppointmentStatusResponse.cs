namespace MicroEMR.Application.Scheduling.Contracts;

public sealed class UpdateAppointmentStatusResponse
{
    public Guid AppointmentUid { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
