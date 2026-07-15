namespace MicroEMR.Application.PatientEncounters;

public sealed class AppointmentCancelledException : Exception
{
    public AppointmentCancelledException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
