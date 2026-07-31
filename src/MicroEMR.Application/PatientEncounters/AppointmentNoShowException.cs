namespace MicroEMR.Application.PatientEncounters;

public sealed class AppointmentNoShowException : Exception
{
    public AppointmentNoShowException(string message, Exception? innerException = null) : base(message, innerException) { }
}
