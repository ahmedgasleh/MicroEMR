namespace MicroEMR.Web.Models.Scheduling;

public sealed class CreateSchedulingBlockedTimeViewModel
{
    public Guid ResourceUid { get; set; }
    public string? Date { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? Reason { get; set; }
}
