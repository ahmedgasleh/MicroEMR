namespace MicroEMR.Application.PatientEncounters;

public sealed class AppointmentCannotStartEncounterException(
    string message,
    Exception? innerException = null)
    : Exception(message, innerException);
