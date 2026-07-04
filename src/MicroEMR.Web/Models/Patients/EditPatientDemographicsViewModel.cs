using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.Patients;

public sealed class EditPatientDemographicsViewModel : IValidatableObject
{
    public Guid PatientUid { get; set; }

    public string ChartNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? MiddleName { get; set; }

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? PreferredName { get; set; }

    [Required]
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20)]
    public string? SexAtBirth { get; set; }

    [StringLength(50)]
    public string? GenderIdentity { get; set; }

    [StringLength(50)]
    public string? HealthCardNumber { get; set; }

    [StringLength(10)]
    public string? HealthCardVersion { get; set; }

    [Phone]
    [StringLength(30)]
    public string? PhoneNumber { get; set; }

    [Phone]
    [StringLength(30)]
    public string? AlternatePhoneNumber { get; set; }

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [StringLength(255)]
    public string? AddressLine1 { get; set; }

    [StringLength(255)]
    public string? AddressLine2 { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(50)]
    public string? Province { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [Required]
    [StringLength(2)]
    public string CountryCode { get; set; } = "CA";

    public bool IsActive { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (PatientUid == Guid.Empty)
        {
            yield return new ValidationResult(
                "A patient is required.",
                new[] { nameof(PatientUid) });
        }

        if (DateOfBirth.HasValue &&
            DateOfBirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            yield return new ValidationResult(
                "Date of birth cannot be in the future.",
                new[] { nameof(DateOfBirth) });
        }
    }
}
