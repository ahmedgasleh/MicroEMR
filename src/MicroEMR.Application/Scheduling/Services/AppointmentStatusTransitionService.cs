namespace MicroEMR.Application.Scheduling.Services;

public sealed class AppointmentStatusTransitionService : IAppointmentStatusTransitionService
{
    private static readonly IReadOnlyDictionary<AppointmentStatus, AppointmentStatus[]> Transitions =
        new Dictionary<AppointmentStatus, AppointmentStatus[]>
        {
            [AppointmentStatus.Scheduled] = [AppointmentStatus.Confirmed, AppointmentStatus.Arrived, AppointmentStatus.Cancelled, AppointmentStatus.NoShow],
            [AppointmentStatus.Confirmed] = [AppointmentStatus.Arrived, AppointmentStatus.CheckedIn, AppointmentStatus.Cancelled, AppointmentStatus.NoShow],
            [AppointmentStatus.Arrived] = [AppointmentStatus.CheckedIn, AppointmentStatus.Roomed, AppointmentStatus.Cancelled],
            [AppointmentStatus.CheckedIn] = [AppointmentStatus.Roomed, AppointmentStatus.Seen],
            [AppointmentStatus.Roomed] = [AppointmentStatus.Seen],
            [AppointmentStatus.Seen] = [AppointmentStatus.Completed]
        };

    public bool CanTransition(AppointmentStatus currentStatus, AppointmentStatus targetStatus) =>
        Transitions.TryGetValue(currentStatus, out var allowed) && allowed.Contains(targetStatus);

    public void EnsureCanTransition(AppointmentStatus currentStatus, AppointmentStatus targetStatus)
    {
        if (!CanTransition(currentStatus, targetStatus))
            throw new AppointmentStatusTransitionException(currentStatus, targetStatus);
    }

    public IReadOnlyList<AppointmentStatus> GetAllowedTransitions(AppointmentStatus currentStatus) =>
        Transitions.TryGetValue(currentStatus, out var allowed) ? allowed : [];
}
