using System.ComponentModel.DataAnnotations;
namespace MicroEMR.Application.PatientVitals.Contracts;
public class CreatePatientVitalRequest
{
    [Required] public DateTime RecordedAt { get; set; }
    [Range(40,300)] public int? BloodPressureSystolic { get; set; }
    [Range(20,200)] public int? BloodPressureDiastolic { get; set; }
    [Range(20,250)] public int? HeartRate { get; set; }
    [Range(5,80)] public int? RespiratoryRate { get; set; }
    [Range(typeof(decimal), "25", "45")] public decimal? TemperatureCelsius { get; set; }
    [Range(0,100)] public int? OxygenSaturation { get; set; }
    [Range(typeof(decimal), "20", "260")] public decimal? HeightCm { get; set; }
    [Range(typeof(decimal), "1", "500")] public decimal? WeightKg { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}
