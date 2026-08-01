namespace MicroEMR.Application.PatientEncounters;

public sealed class LinkedAppointmentNotFoundException(
    string message,
    Exception? innerException = null) : Exception(message, innerException);
