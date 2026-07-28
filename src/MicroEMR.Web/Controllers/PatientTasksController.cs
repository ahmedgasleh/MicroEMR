using System.Net;using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using MicroEMR.Web.Models.PatientTasks;using MicroEMR.Web.Services.PatientTasks;
namespace MicroEMR.Web.Controllers;
[Authorize]public sealed class PatientTasksController:Controller
{
 private readonly IPatientTaskApiClient _client;private readonly ILogger<PatientTasksController>_logger;public PatientTasksController(IPatientTaskApiClient client,ILogger<PatientTasksController>logger){_client=client;_logger=logger;}
 [HttpGet]public async Task<IActionResult>List(Guid patientUid,string status="Open",CancellationToken t=default){if(patientUid==Guid.Empty||status is not("Open"or"Completed"or"All"))return BadRequest(new{success=false,message="Invalid task filter."});return Json(new{success=true,tasks=await _client.GetPatientTasksAsync(patientUid,status,t)});}
 [HttpPost,ValidateAntiForgeryToken]public Task<IActionResult>Create(CreatePatientTaskViewModel model,CancellationToken t)=>Save(model,()=>_client.CreatePatientTaskAsync(model.PatientUid,model,t));
 [HttpPost,ValidateAntiForgeryToken]public Task<IActionResult>Update(UpdatePatientTaskViewModel model,CancellationToken t)=>Save(model,()=>_client.UpdatePatientTaskAsync(model.PatientUid,model.PatientTaskUid,model,t));
 [HttpPost,ValidateAntiForgeryToken]public Task<IActionResult>Complete(CompletePatientTaskViewModel model,CancellationToken t)=>Save(model,()=>_client.CompletePatientTaskAsync(model.PatientUid,model.PatientTaskUid,model,t));
 [HttpPost,ValidateAntiForgeryToken]public Task<IActionResult>Reopen(Guid patientUid,Guid patientTaskUid,CancellationToken t)=>Save(null,()=>_client.ReopenPatientTaskAsync(patientUid,patientTaskUid,t));
 private async Task<IActionResult>Save(object? model,Func<Task<PatientTaskViewModel?>>action){if(!ModelState.IsValid)return BadRequest(new{success=false,message="Please correct the task information."});try{var task=await action();return task is null?NotFound(new{success=false,message="Task was not found."}):Json(new{success=true,task});}catch(HttpRequestException e)when(e.StatusCode==HttpStatusCode.Conflict){return Conflict(new{success=false,message=e.Message});}catch(Exception e){_logger.LogError(e,"Patient task web operation failed.");return StatusCode(502,new{success=false,message="The task operation could not be completed."});}}
}
