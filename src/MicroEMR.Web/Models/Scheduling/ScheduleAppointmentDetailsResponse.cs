namespace MicroEMR.Web.Models.Scheduling;

public sealed class ScheduleAppointmentDetailsResponse
{
    public Guid AppointmentUid { get; set; }
    public Guid PatientUid { get; set; }
    public Guid PrimaryResourceUid { get; set; }
    public Guid? RoomResourceUid { get; set; }
    public DateTime StartDateTimeUtc { get; set; }
    public DateTime EndDateTimeUtc { get; set; }
    public string? AppointmentType { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PatientDisplayName { get; set; } = string.Empty;
    public string ChartNumber { get; set; } = string.Empty;
    public string PrimaryResourceName { get; set; } = string.Empty;
    public string? RoomResourceName { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? RowVersion { get; set; }
}
