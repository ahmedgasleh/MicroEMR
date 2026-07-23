namespace MicroEMR.Application.PatientVitals.Contracts;
public sealed class PatientVitalResponse
{
    public Guid PatientVitalUid { get; set; }
    public Guid PatientUid { get; set; }
    public DateTime RecordedAt { get; set; }
    public int? BloodPressureSystolic { get; set; }
    public int? BloodPressureDiastolic { get; set; }
    public int? HeartRate { get; set; }
    public int? RespiratoryRate { get; set; }
    public decimal? TemperatureCelsius { get; set; }
    public int? OxygenSaturation { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? Bmi { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public string? UpdatedByDisplayName { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
