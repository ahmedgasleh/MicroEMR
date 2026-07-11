namespace MicroEMR.Application.Scheduling.Contracts;

public sealed class ScheduleMonthSummaryItemResponse
{
    public DateTime Date { get; set; }
    public int AppointmentCount { get; set; }
    public int ProviderCount { get; set; }
    public string Status { get; set; } = string.Empty;
}
