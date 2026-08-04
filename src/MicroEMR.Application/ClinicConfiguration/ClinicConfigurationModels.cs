using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.ClinicConfiguration;

public sealed record ClinicProfileData(
    string? LegalName,
    string? Phone,
    string? Fax,
    string? Email,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? ProvinceState,
    string? PostalCode,
    string? Country,
    int? DefaultAppointmentDurationMinutes,
    DateTime? UpdatedAtUtc,
    long? UpdatedBy,
    string? RowVersion);

public sealed record ClinicConfigurationResponse(
    string ClinicName,
    string TimeZoneId,
    string? LegalName,
    string? Phone,
    string? Fax,
    string? Email,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? ProvinceState,
    string? PostalCode,
    string? Country,
    int? DefaultAppointmentDurationMinutes,
    DateTime? UpdatedAtUtc,
    long? UpdatedBy,
    string? RowVersion);

public sealed class SaveClinicConfigurationRequest
{
    [StringLength(200)] public string? LegalName { get; set; }
    [StringLength(50)] public string? Phone { get; set; }
    [StringLength(50)] public string? Fax { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }
    [StringLength(200)] public string? AddressLine1 { get; set; }
    [StringLength(200)] public string? AddressLine2 { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(100)] public string? ProvinceState { get; set; }
    [StringLength(30)] public string? PostalCode { get; set; }
    [StringLength(100)] public string? Country { get; set; }
    [Range(5, 240)] public int? DefaultAppointmentDurationMinutes { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class ClinicConfigurationConcurrencyException(string message, Exception? inner = null)
    : Exception(message, inner);
