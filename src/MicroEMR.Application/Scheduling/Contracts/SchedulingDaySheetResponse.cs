namespace MicroEMR.Application.Scheduling.Contracts;

public sealed class SchedulingDaySheetRequest
{
    public DateOnly Date { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    // Null means all active clinicians; an empty selection is invalid.
    public Guid[]? ClinicianUids { get; set; }
}

public sealed class SchedulingDaySheetResponse
{
    public DateOnly Date { get; set; }
    public string ClinicianScope { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public IReadOnlyList<SchedulingDaySheetRow> Appointments { get; set; } = [];
}

public sealed class SchedulingDaySheetRow
{
    public string PatientName { get; set; } = string.Empty;
    public DateTime StartDateTimeUtc { get; set; }
    public DateTime EndDateTimeUtc { get; set; }
    public string ClinicianName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
