namespace MicroEMR.Application.PatientEncounters;

public sealed class EncounterAddendumNotAllowedException : Exception
{
    public EncounterAddendumNotAllowedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
