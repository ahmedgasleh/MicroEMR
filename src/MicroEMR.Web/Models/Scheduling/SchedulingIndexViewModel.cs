namespace MicroEMR.Web.Models.Scheduling;

public sealed class SchedulingIndexViewModel
{
    public IReadOnlyList<ScheduleResourceResponse> Resources { get; set; }
        = Array.Empty<ScheduleResourceResponse>();
}
