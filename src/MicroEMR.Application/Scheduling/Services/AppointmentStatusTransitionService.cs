namespace MicroEMR.Application.Scheduling.Services;

public sealed class AppointmentStatusTransitionService : IAppointmentStatusTransitionService
{
    private static readonly IReadOnlyDictionary<AppointmentStatus, IReadOnlySet<AppointmentStatus>> AllowedTransitions =
        new Dictionary<AppointmentStatus, IReadOnlySet<AppointmentStatus>>
        {
            [AppointmentStatus.Scheduled] = new HashSet<AppointmentStatus>
            {
                AppointmentStatus.Confirmed,
                AppointmentStatus.Arrived,
                AppointmentStatus.Seen,
                AppointmentStatus.Cancelled,
                AppointmentStatus.NoShow
            },
            [AppointmentStatus.Confirmed] = new HashSet<AppointmentStatus>
            {
                AppointmentStatus.Arrived,
                AppointmentStatus.CheckedIn,
                AppointmentStatus.Cancelled,
                AppointmentStatus.NoShow
            },
            [AppointmentStatus.Arrived] = new HashSet<AppointmentStatus>
            {
                AppointmentStatus.CheckedIn,
                AppointmentStatus.Roomed,
                AppointmentStatus.Seen,
                AppointmentStatus.Cancelled
            },
            [AppointmentStatus.CheckedIn] = new HashSet<AppointmentStatus>
            {
                AppointmentStatus.Roomed,
                AppointmentStatus.Seen
            },
            [AppointmentStatus.Roomed] = new HashSet<AppointmentStatus>
            {
                AppointmentStatus.Seen
            },
            [AppointmentStatus.Seen] = new HashSet<AppointmentStatus>
            {
                AppointmentStatus.Completed
            }
        };

    public bool CanTransition(AppointmentStatus currentStatus, AppointmentStatus targetStatus) =>
        AllowedTransitions.TryGetValue(currentStatus, out var targets) && targets.Contains(targetStatus);

    public void EnsureCanTransition(AppointmentStatus currentStatus, AppointmentStatus targetStatus)
    {
        if (!CanTransition(currentStatus, targetStatus))
        {
            throw new AppointmentStatusTransitionException(currentStatus, targetStatus);
        }
    }
}
