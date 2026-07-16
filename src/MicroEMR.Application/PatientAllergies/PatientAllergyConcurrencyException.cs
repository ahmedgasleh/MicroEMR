namespace MicroEMR.Application.PatientAllergies;

public sealed class PatientAllergyConcurrencyException : Exception
{
    public PatientAllergyConcurrencyException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
