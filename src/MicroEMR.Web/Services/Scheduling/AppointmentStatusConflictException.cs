namespace MicroEMR.Web.Services.Scheduling;

public sealed class AppointmentStatusConflictException : Exception
{
    public AppointmentStatusConflictException()
        : base("Cancelled appointments cannot be updated.")
    {
    }
}
