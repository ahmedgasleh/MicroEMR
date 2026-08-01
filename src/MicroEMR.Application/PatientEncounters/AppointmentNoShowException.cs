namespace MicroEMR.Application.PatientEncounters;

public sealed class AppointmentNoShowException(string message, Exception? innerException = null)
    : Exception(message, innerException);
