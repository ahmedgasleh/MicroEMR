namespace MicroEMR.Web.Models.Scheduling;

public sealed class AppointmentHistoryResponse
{
    public Guid AppointmentHistoryUid { get; set; }
    public Guid AppointmentUid { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? ActionDescription { get; set; }
    public DateTime? OldStartDateTimeUtc { get; set; }
    public DateTime? NewStartDateTimeUtc { get; set; }
    public DateTime? OldEndDateTimeUtc { get; set; }
    public DateTime? NewEndDateTimeUtc { get; set; }
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public Guid? OldResourceUid { get; set; }
    public Guid? NewResourceUid { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }
}
