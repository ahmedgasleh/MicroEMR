namespace MicroEMR.Application.Scheduling;

public sealed class AppointmentAlreadyCancelledException : Exception
{
    public AppointmentAlreadyCancelledException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
