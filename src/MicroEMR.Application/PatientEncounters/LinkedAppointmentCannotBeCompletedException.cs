namespace MicroEMR.Application.PatientEncounters;

public sealed class LinkedAppointmentCannotBeCompletedException(
    string message,
    Exception? innerException = null) : Exception(message, innerException);
