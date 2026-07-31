namespace MicroEMR.Application.PatientEncounters;

public sealed class EncounterSigningValidationException : Exception
{
    public EncounterSigningValidationException(IReadOnlyList<string> errors)
        : base(string.Join(" ", errors)) => Errors = errors;

    public IReadOnlyList<string> Errors { get; }
}
