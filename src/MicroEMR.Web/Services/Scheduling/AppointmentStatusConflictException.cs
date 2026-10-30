namespace MicroEMR.Web.Services.Scheduling;

public sealed class AppointmentStatusConflictException : Exception
{
    public AppointmentStatusConflictException()
        : base("The appointment status cannot be changed from its current state. Refresh and try again.")
    {
    }
}
