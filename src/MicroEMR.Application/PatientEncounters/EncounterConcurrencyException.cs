namespace MicroEMR.Application.PatientEncounters;

public sealed class EncounterConcurrencyException : Exception
{
    public EncounterConcurrencyException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
