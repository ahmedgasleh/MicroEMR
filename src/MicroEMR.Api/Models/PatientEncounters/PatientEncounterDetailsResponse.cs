namespace MicroEMR.Api.Models.PatientEncounters;

public sealed class PatientEncounterDetailsResponse
{
    public Guid EncounterUid { get; set; }

    public Guid PatientUid { get; set; }

    public DateTime EncounterDateUtc { get; set; }

    public string EncounterType { get; set; } = string.Empty;

    public string? ReasonForVisit { get; set; }

    public string? LocationName { get; set; }

    public string? ProviderName { get; set; }

    public string Status { get; set; } = string.Empty;

    public long? CreatedBy { get; set; }

    public string? CreatedByDisplayName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}
