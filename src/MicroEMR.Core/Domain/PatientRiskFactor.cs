namespace MicroEMR.Core.Domain;

public record PatientRiskFactor
{
    public Guid RiskFactorUid { get; init; }
    public Guid PatientUid { get; init; }
    public string RiskFactor { get; init; } = "";
    public string? ExposureDetails { get; init; }
    public string? LifeStageCode { get; init; }
    public string? Notes { get; init; }
    public string Status { get; init; } = "Active";
    public DateTime CreatedAt { get; init; }
    public long CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public long? UpdatedBy { get; init; }
    public string RowVersion { get; init; } = "";
}
