namespace MicroEMR.Application.Scheduling.Contracts;

public sealed class SchedulingBlockedTimeResponse
{
    public Guid BlockedTimeUid { get; set; }
    public Guid ResourceUid { get; set; }
    public DateTime StartDateTimeUtc { get; set; }
    public DateTime EndDateTimeUtc { get; set; }
    public string? Reason { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }
}
