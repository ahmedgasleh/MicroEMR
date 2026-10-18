namespace MicroEMR.Application.PatientAllergies.Contracts;
public sealed class CreatePatientAllergyRequest : AllergySaveRequest
{
    public bool ConfirmReplaceNoKnownAllergies { get; set; }
    public Guid? ExpectedAssertionUid { get; set; }
    public string? ExpectedAssertionRowVersion { get; set; }
}
