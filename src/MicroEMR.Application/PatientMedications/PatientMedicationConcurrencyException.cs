namespace MicroEMR.Application.PatientMedications;

public sealed class PatientMedicationConcurrencyException : Exception
{
    public PatientMedicationConcurrencyException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
