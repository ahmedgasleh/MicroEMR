namespace MicroEMR.Application.Scheduling;

public enum AppointmentStatus
{
    Scheduled,
    Confirmed,
    Arrived,
    CheckedIn,
    Roomed,
    Seen,
    Completed,
    Cancelled,
    NoShow
}

public static class AppointmentStatusCatalog
{
    public static AppointmentStatus Parse(string value)
    {
        if (string.Equals(value, "Booked", StringComparison.OrdinalIgnoreCase))
            return AppointmentStatus.Scheduled;
        if (Enum.TryParse<AppointmentStatus>(value, true, out var status)
            && Enum.IsDefined(status))
            return status;
        throw new ArgumentException("Unknown appointment status.", nameof(value));
    }

    public static string ToStoredValue(AppointmentStatus status) => status.ToString();

    public static string GetLabel(AppointmentStatus status) => status switch
    {
        AppointmentStatus.CheckedIn => "Checked In",
        AppointmentStatus.NoShow => "No Show",
        AppointmentStatus.Roomed => "In Room",
        AppointmentStatus.Seen => "Encounter Started",
        _ => status.ToString()
    };
}
