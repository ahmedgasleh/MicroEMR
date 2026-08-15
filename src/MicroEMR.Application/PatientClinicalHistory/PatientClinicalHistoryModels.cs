using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientClinicalHistory;

public sealed class PatientClinicalHistoryResponse
{
    public Guid HistoryUid { get; init; }
    public Guid PatientUid { get; init; }
    public required string HistoryType { get; init; }
    public required string Description { get; init; }
    public DateOnly? RelevantDate { get; init; }
    public required string Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public long CreatedBy { get; init; }
    public string? CreatedByDisplayName { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public long? UpdatedBy { get; init; }
    public string? UpdatedByDisplayName { get; init; }
    public required string RowVersion { get; init; }
}

public sealed class CreatePatientClinicalHistoryRequest : IValidatableObject
{
    [Required, StringLength(20)] public string HistoryType { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Description { get; set; } = string.Empty;
    public DateOnly? RelevantDate { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => ValidateValues(HistoryType, Description, RelevantDate);
    internal static IEnumerable<ValidationResult> ValidateValues(string? type, string? description, DateOnly? date)
    {
        if (type is not ("Medical" or "Surgical")) yield return new("History type must be Medical or Surgical.", [nameof(HistoryType)]);
        if (string.IsNullOrWhiteSpace(description)) yield return new("Description is required.", [nameof(Description)]);
        if (date > DateOnly.FromDateTime(DateTime.UtcNow)) yield return new("Relevant date cannot be in the future.", [nameof(RelevantDate)]);
    }
}

public sealed class UpdatePatientClinicalHistoryRequest : IValidatableObject
{
    [Required, StringLength(20)] public string HistoryType { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Description { get; set; } = string.Empty;
    public DateOnly? RelevantDate { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => CreatePatientClinicalHistoryRequest.ValidateValues(HistoryType, Description, RelevantDate);
}

public sealed class ArchivePatientClinicalHistoryRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class PatientClinicalHistoryConcurrencyException : Exception;
public sealed class PatientClinicalHistoryArchivedException : Exception;
