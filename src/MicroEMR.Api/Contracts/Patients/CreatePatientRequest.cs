using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Api.Contracts.Patients;

public sealed class CreatePatientRequest
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? MiddleName { get; set; }

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20)]
    public string? SexAtBirth { get; set; }

    [StringLength(50)]
    public string? GenderIdentity { get; set; }

    [StringLength(100)]
    public string? PreferredName { get; set; }

    [StringLength(50)]
    public string? HealthCardNumber { get; set; }

    [StringLength(10)]
    public string? HealthCardVersion { get; set; }

    [StringLength(30)]
    public string? PhoneNumber { get; set; }

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

    [StringLength(2)]
    public string CountryCode { get; set; } = "CA";
}