namespace MicroEMR.Application.Scheduling;

public sealed class AppointmentConcurrencyException : Exception
{
    public AppointmentConcurrencyException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
