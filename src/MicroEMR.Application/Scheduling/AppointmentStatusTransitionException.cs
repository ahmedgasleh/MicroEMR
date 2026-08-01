namespace MicroEMR.Application.Scheduling;

public sealed class AppointmentStatusTransitionException : InvalidOperationException
{
    public AppointmentStatusTransitionException(
        AppointmentStatus currentStatus,
        AppointmentStatus targetStatus)
        : base($"Appointment cannot transition from {currentStatus} to {targetStatus}.")
    {
        CurrentStatus = currentStatus;
        TargetStatus = targetStatus;
    }

    public AppointmentStatus CurrentStatus { get; }

    public AppointmentStatus TargetStatus { get; }
}
