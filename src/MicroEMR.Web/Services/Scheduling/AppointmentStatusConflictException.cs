namespace MicroEMR.Web.Services.Scheduling;

public sealed class AppointmentStatusConflictException : Exception
{
    public AppointmentStatusConflictException(string message)
        : base(message)
    {
    }
}
