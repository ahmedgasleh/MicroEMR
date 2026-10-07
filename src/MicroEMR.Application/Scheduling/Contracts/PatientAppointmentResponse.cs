namespace MicroEMR.Application.Scheduling.Contracts;

public sealed class PatientAppointmentResponse
{
    public Guid AppointmentUid { get; set; }
    public Guid PatientUid { get; set; }
    public DateTime StartDateTimeUtc { get; set; }
    public DateTime EndDateTimeUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AppointmentType { get; set; }
    public string? PrimaryResourceName { get; set; }
    public string? Reason { get; set; }
}
