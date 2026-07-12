namespace MicroEMR.Web.Models.Dashboard;

public sealed class DashboardViewModel
{
    public IReadOnlyList<DashboardAppointmentViewModel> TodaysAppointments { get; set; }
        = Array.Empty<DashboardAppointmentViewModel>();

    public int TodaysAppointmentCount { get; set; }

    public bool ScheduleLoadFailed { get; set; }
}

public sealed class DashboardAppointmentViewModel
{
    public Guid AppointmentUid { get; set; }

    public Guid PatientUid { get; set; }

    public string PatientDisplayName { get; set; } = string.Empty;

    public string? ChartNumber { get; set; }

    public DateTime StartDateTimeLocal { get; set; }

    public DateTime EndDateTimeLocal { get; set; }

    public string? PrimaryResourceName { get; set; }

    public string? AppointmentType { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = string.Empty;
}
