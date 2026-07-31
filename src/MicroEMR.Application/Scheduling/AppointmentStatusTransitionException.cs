namespace MicroEMR.Application.Scheduling;

public sealed class AppointmentStatusTransitionException : Exception
{
    public AppointmentStatusTransitionException(AppointmentStatus current, AppointmentStatus target,
        Exception? innerException = null)
        : base($"Appointment cannot transition from {AppointmentStatusCatalog.GetLabel(current)} to {AppointmentStatusCatalog.GetLabel(target)}.", innerException)
    {
        Current = current;
        Target = target;
    }

    public AppointmentStatus Current { get; }
    public AppointmentStatus Target { get; }
}
