namespace MicroEMR.Application.Scheduling;

public sealed class AppointmentTerminalStateException : Exception
{
    public AppointmentTerminalStateException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
