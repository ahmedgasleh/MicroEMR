using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.Patients;

public sealed class PatientAlternativeContact : IValidatableObject
{
    [StringLength(100)] public string? FirstName { get; set; }
    [StringLength(100)] public string? LastName { get; set; }
    public List<string> Purposes { get; set; } = [];
    [Phone, StringLength(30)] public string? ResidencePhone { get; set; }
    [Phone, StringLength(30)] public string? CellPhone { get; set; }
    [Phone, StringLength(30)] public string? WorkPhone { get; set; }
    [StringLength(20)] public string? WorkPhoneExtension { get; set; }
    [EmailAddress, StringLength(255)] public string? Email { get; set; }
    [StringLength(1000)] public string? Note { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FirstName))
            yield return new ValidationResult("Contact first name is required.", [nameof(FirstName)]);
        if (string.IsNullOrWhiteSpace(LastName))
            yield return new ValidationResult("Contact last name is required.", [nameof(LastName)]);
        if (Purposes is null || Purposes.Count == 0 || Purposes.Any(value =>
            value is not ("Emergency Contact" or "Substitute Decision Maker")))
            yield return new ValidationResult("Select a supported contact purpose.", [nameof(Purposes)]);
    }
}
