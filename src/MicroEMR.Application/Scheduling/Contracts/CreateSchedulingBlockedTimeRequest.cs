namespace MicroEMR.Application.Scheduling.Contracts;

public sealed class CreateSchedulingBlockedTimeRequest
{
    public Guid ResourceUid { get; set; }
    public DateTime StartDateTimeUtc { get; set; }
    public DateTime EndDateTimeUtc { get; set; }
    public string? Reason { get; set; }
}
