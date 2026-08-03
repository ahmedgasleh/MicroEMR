namespace MicroEMR.Application.PatientVitals;

public sealed class PatientVitalConcurrencyException : Exception
{
    public PatientVitalConcurrencyException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
