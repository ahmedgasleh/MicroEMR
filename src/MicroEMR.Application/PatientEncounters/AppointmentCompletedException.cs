namespace MicroEMR.Application.PatientEncounters;

public sealed class AppointmentCompletedException : Exception
{
    public AppointmentCompletedException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
