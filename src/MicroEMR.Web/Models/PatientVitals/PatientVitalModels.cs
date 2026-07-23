using System.ComponentModel.DataAnnotations;
namespace MicroEMR.Web.Models.PatientVitals;
public class PatientVitalViewModel
{
    public Guid PatientVitalUid { get;set;} public Guid PatientUid{get;set;} public DateTime RecordedAt{get;set;}
    public int? BloodPressureSystolic{get;set;} public int? BloodPressureDiastolic{get;set;} public int? HeartRate{get;set;} public int? RespiratoryRate{get;set;}
    public decimal? TemperatureCelsius{get;set;} public int? OxygenSaturation{get;set;} public decimal? HeightCm{get;set;} public decimal? WeightKg{get;set;} public decimal? Bmi{get;set;}
    public string? Notes{get;set;} public DateTime CreatedAt{get;set;} public long? CreatedBy{get;set;} public string? CreatedByDisplayName{get;set;} public DateTime? UpdatedAt{get;set;} public long? UpdatedBy{get;set;} public string? UpdatedByDisplayName{get;set;} public string RowVersion{get;set;}=string.Empty;
}
public class CreatePatientVitalViewModel
{
    public Guid PatientUid{get;set;} [Required] public DateTime RecordedAt{get;set;}
    [Range(40,300)] public int? BloodPressureSystolic{get;set;} [Range(20,200)] public int? BloodPressureDiastolic{get;set;}
    [Range(20,250)] public int? HeartRate{get;set;} [Range(5,80)] public int? RespiratoryRate{get;set;}
    [Range(typeof(decimal),"25","45")] public decimal? TemperatureCelsius{get;set;} [Range(0,100)] public int? OxygenSaturation{get;set;}
    [Range(typeof(decimal),"20","260")] public decimal? HeightCm{get;set;} [Range(typeof(decimal),"1","500")] public decimal? WeightKg{get;set;}
    [StringLength(1000)] public string? Notes{get;set;}
}
public sealed class UpdatePatientVitalViewModel:CreatePatientVitalViewModel { public Guid PatientVitalUid{get;set;} }
public sealed class CreatePatientVitalRequest:CreatePatientVitalViewModel { }
public sealed class UpdatePatientVitalRequest:CreatePatientVitalViewModel { }
