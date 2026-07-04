namespace MicroEMR.Application.PatientAllergies.Contracts;

public sealed class PatientAllergyDetailsResponse
{
    public Guid AllergyUid { get; set; }

    public Guid PatientUid { get; set; }

    public string AllergenName { get; set; } = string.Empty;

    public string? AllergenType { get; set; }

    public string? Reaction { get; set; }

    public string? Severity { get; set; }

    public DateTime? OnsetDate { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = string.Empty;

    public long? CreatedBy { get; set; }

    public string? CreatedByDisplayName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}
