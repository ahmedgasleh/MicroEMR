using MicroEMR.Application.Reporting;

namespace MicroEMR.Web.Models.Reporting;

public sealed class AppointmentStatusReportViewModel
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public AppointmentStatusReport? Report { get; init; }
    public string? ErrorMessage { get; init; }
}
