namespace MicroEMR.Application.PatientEncounters;

public sealed class EncounterNoteNotEditableException : Exception
{
    public EncounterNoteNotEditableException(
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
