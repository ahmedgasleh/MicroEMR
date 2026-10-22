namespace MicroEMR.Application.PatientMedications.Contracts;

// Presence of the group distinguishes deliberate unknown from an old client's omission.
public sealed class MedicationPlanFields
{
    public bool? LongTermIntent { get; set; }
}
