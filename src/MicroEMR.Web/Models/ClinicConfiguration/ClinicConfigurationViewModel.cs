using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.ClinicConfiguration;

public sealed class ClinicConfigurationViewModel
{
    [Display(Name = "Clinic name")]
    public string ClinicName { get; set; } = string.Empty;

    [Display(Name = "Time zone")]
    public string TimeZoneId { get; set; } = string.Empty;

    [Display(Name = "Legal name"), StringLength(200)] public string? LegalName { get; set; }
    [StringLength(50)] public string? Phone { get; set; }
    [StringLength(50)] public string? Fax { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }
    [Display(Name = "Address line 1"), StringLength(200)] public string? AddressLine1 { get; set; }
    [Display(Name = "Address line 2"), StringLength(200)] public string? AddressLine2 { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [Display(Name = "Province/State"), StringLength(100)] public string? ProvinceState { get; set; }
    [Display(Name = "Postal code"), StringLength(30)] public string? PostalCode { get; set; }
    [StringLength(100)] public string? Country { get; set; }

    [Display(Name = "Default appointment duration (minutes)")]
    [Range(5, 240)]
    public int? DefaultAppointmentDurationMinutes { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
    public long? UpdatedBy { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class SaveClinicConfigurationRequest
{
    public string? LegalName { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? ProvinceState { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public int? DefaultAppointmentDurationMinutes { get; set; }
    public string? RowVersion { get; set; }
}
