namespace MicroEMR.Application.PatientEncounters;

public sealed class EncounterCannotBeSignedException : Exception
{
    public EncounterCannotBeSignedException(
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
