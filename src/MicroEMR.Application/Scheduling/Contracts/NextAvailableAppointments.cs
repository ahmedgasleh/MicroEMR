namespace MicroEMR.Application.Scheduling.Contracts;

public sealed class NextAvailableAppointmentsRequest
{
    public Guid ClinicianUid { get; set; }
    public Guid? RoomUid { get; set; }
    public DateOnly StartDate { get; set; }
    public int HorizonDays { get; set; } = 30;
    public int[]? Weekdays { get; set; }
    public TimeOnly? PreferredStart { get; set; }
    public TimeOnly? PreferredEnd { get; set; }
    public string? AppointmentType { get; set; }
    public int DurationMinutes { get; set; } = 15;
    // Web supplies the same server-local zone used by the existing booking workflow.
    public string? TimeZoneId { get; set; }
}

public sealed class NextAvailableAppointmentsResponse
{
    public string TimeZoneId { get; set; } = string.Empty;
    public IReadOnlyList<AvailableAppointmentSlot> Slots { get; set; } = [];
}

public sealed record AvailableAppointmentSlot(Guid ClinicianUid, Guid? RoomUid, string? AppointmentType,
    DateTime StartDateTimeUtc, DateTime EndDateTimeUtc, DateTime StartDateTimeLocal, DateTime EndDateTimeLocal);

public sealed record SchedulingBusyPeriod(DateTime StartDateTimeUtc, DateTime EndDateTimeUtc);

public sealed class NextAvailableSearchOptions
{
    public int MaxHorizonDays { get; set; } = 90;
}
