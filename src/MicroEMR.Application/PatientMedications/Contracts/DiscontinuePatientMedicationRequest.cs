using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientMedications.Contracts;

public sealed class DiscontinuePatientMedicationRequest : IValidatableObject
{
    [StringLength(500)] public string? DiscontinueReason { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;

    public byte[] GetRowVersionBytes()
    {
        if (string.IsNullOrWhiteSpace(RowVersion)) throw new FormatException("RowVersion is required.");
        var bytes = Convert.FromBase64String(RowVersion);
        if (bytes.Length != 8) throw new FormatException("RowVersion must contain eight bytes.");
        return bytes;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var valid = true;
        try { GetRowVersionBytes(); }
        catch (FormatException) { valid = false; }
        if (!valid) yield return new ValidationResult("The row version is invalid.", [nameof(RowVersion)]);
    }
}
