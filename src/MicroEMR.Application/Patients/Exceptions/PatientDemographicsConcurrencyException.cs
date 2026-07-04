namespace MicroEMR.Application.Patients.Exceptions;

public sealed class PatientDemographicsConcurrencyException : Exception
{
    public PatientDemographicsConcurrencyException()
        : base(
            "This patient was updated by another user. Reload the patient and try again.")
    {
    }
}
