using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroEMR.Web.Models.PatientVitals;
using MicroEMR.Web.Services.PatientVitals;
namespace MicroEMR.Web.Controllers;
[Authorize]
public sealed class PatientVitalsController(IPatientVitalApiClient client,ILogger<PatientVitalsController> logger):Controller
{
 [HttpGet] public async Task<IActionResult> Vitals(Guid patientUid,CancellationToken ct=default)=>patientUid==Guid.Empty?BadRequest():Json(await client.GetPatientVitalsAsync(patientUid,ct));
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> CreateVital(CreatePatientVitalViewModel model,CancellationToken ct){if(model.PatientUid==Guid.Empty||!ModelState.IsValid)return BadRequest(Failure(First()));try{var vital=await client.CreatePatientVitalAsync(model.PatientUid,Map(model),ct);return vital is null?NotFound(Failure("Patient was not found.")):Json(new{success=true,message="Vitals saved.",vital});}catch(Exception ex)when(ex is HttpRequestException or UnauthorizedAccessException){logger.LogError(ex,"Unable to create patient vitals.");return StatusCode(502,Failure("Vitals could not be saved."));}}
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> UpdateVital(UpdatePatientVitalViewModel model,CancellationToken ct){if(model.PatientUid==Guid.Empty||model.PatientVitalUid==Guid.Empty||!ModelState.IsValid)return BadRequest(Failure(First()));try{var vital=await client.UpdatePatientVitalAsync(model.PatientUid,model.PatientVitalUid,MapUpdate(model),ct);return vital is null?NotFound(Failure("Vitals were not found.")):Json(new{success=true,message="Vitals saved.",vital});}catch(Exception ex)when(ex is HttpRequestException or UnauthorizedAccessException){logger.LogError(ex,"Unable to update patient vitals.");return StatusCode(502,Failure("Vitals could not be saved."));}}
 private static CreatePatientVitalRequest Map(CreatePatientVitalViewModel m)=>new(){PatientUid=m.PatientUid,RecordedAt=m.RecordedAt,BloodPressureSystolic=m.BloodPressureSystolic,BloodPressureDiastolic=m.BloodPressureDiastolic,HeartRate=m.HeartRate,RespiratoryRate=m.RespiratoryRate,TemperatureCelsius=m.TemperatureCelsius,OxygenSaturation=m.OxygenSaturation,HeightCm=m.HeightCm,WeightKg=m.WeightKg,Notes=m.Notes};
 private static UpdatePatientVitalRequest MapUpdate(UpdatePatientVitalViewModel m)=>new(){PatientUid=m.PatientUid,RecordedAt=m.RecordedAt,BloodPressureSystolic=m.BloodPressureSystolic,BloodPressureDiastolic=m.BloodPressureDiastolic,HeartRate=m.HeartRate,RespiratoryRate=m.RespiratoryRate,TemperatureCelsius=m.TemperatureCelsius,OxygenSaturation=m.OxygenSaturation,HeightCm=m.HeightCm,WeightKg=m.WeightKg,Notes=m.Notes};
 private object Failure(string message)=>new{success=false,message}; private string First()=>ModelState.Values.SelectMany(v=>v.Errors).Select(e=>e.ErrorMessage).FirstOrDefault()??"Vitals could not be saved.";
}
