namespace MicroEMR.Application.Scheduling;

public static class AppointmentStatusMapper
{
    private static readonly IReadOnlyDictionary<string, AppointmentStatus> StorageValues =
        new Dictionary<string, AppointmentStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["Scheduled"] = AppointmentStatus.Scheduled,
            ["Booked"] = AppointmentStatus.Scheduled,
            ["Confirmed"] = AppointmentStatus.Confirmed,
            ["Arrived"] = AppointmentStatus.Arrived,
            ["CheckedIn"] = AppointmentStatus.CheckedIn,
            ["Roomed"] = AppointmentStatus.Roomed,
            ["Seen"] = AppointmentStatus.Seen,
            ["Completed"] = AppointmentStatus.Completed,
            ["Cancelled"] = AppointmentStatus.Cancelled,
            ["NoShow"] = AppointmentStatus.NoShow
        };

    public static AppointmentStatus Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (StorageValues.TryGetValue(value.Trim(), out var status))
        {
            return status;
        }

        throw new ArgumentException($"Unknown appointment status '{value}'.", nameof(value));
    }

    public static string ToStorageValue(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Scheduled => "Scheduled",
        AppointmentStatus.Confirmed => "Confirmed",
        AppointmentStatus.Arrived => "Arrived",
        AppointmentStatus.CheckedIn => "CheckedIn",
        AppointmentStatus.Roomed => "Roomed",
        AppointmentStatus.Seen => "Seen",
        AppointmentStatus.Completed => "Completed",
        AppointmentStatus.Cancelled => "Cancelled",
        AppointmentStatus.NoShow => "NoShow",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown appointment status.")
    };
}
