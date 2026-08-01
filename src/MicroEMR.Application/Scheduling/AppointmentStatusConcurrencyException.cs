namespace MicroEMR.Application.Scheduling;

public sealed class AppointmentStatusConcurrencyException(
    string message,
    Exception? innerException = null)
    : InvalidOperationException(message, innerException);
