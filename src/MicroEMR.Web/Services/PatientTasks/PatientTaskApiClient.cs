using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Web.Models.PatientTasks;
namespace MicroEMR.Web.Services.PatientTasks;
public interface IPatientTaskApiClient { Task<IReadOnlyList<PatientTaskViewModel>>GetPatientTasksAsync(Guid patientUid,string status,CancellationToken cancellationToken=default); Task<PatientTaskViewModel?>GetPatientTaskAsync(Guid patientUid,Guid uid,CancellationToken cancellationToken=default); Task<PatientTaskViewModel?>CreatePatientTaskAsync(Guid patientUid,object request,CancellationToken cancellationToken); Task<PatientTaskViewModel?>UpdatePatientTaskAsync(Guid patientUid,Guid uid,object request,CancellationToken cancellationToken); Task<PatientTaskViewModel?>CompletePatientTaskAsync(Guid patientUid,Guid uid,object request,CancellationToken cancellationToken); Task<PatientTaskViewModel?>ReopenPatientTaskAsync(Guid patientUid,Guid uid,CancellationToken cancellationToken); Task<IReadOnlyList<PatientDashboardTaskViewModel>>GetDashboardOpenTasksAsync(int maxRows,CancellationToken cancellationToken=default); Task<int>GetOverdueCountAsync(CancellationToken cancellationToken=default); }
public sealed class PatientTaskApiClient:IPatientTaskApiClient
{
    private readonly HttpClient _client; private readonly IHttpContextAccessor _context;
    public PatientTaskApiClient(HttpClient client,IHttpContextAccessor context){_client=client;_context=context;}
    public async Task<IReadOnlyList<PatientTaskViewModel>>GetPatientTasksAsync(Guid p,string s,CancellationToken t=default){using var q=await Request(HttpMethod.Get,$"api/patients/{p}/tasks?status={Uri.EscapeDataString(s)}");using var r=await _client.SendAsync(q,t);await Ensure(r);return await r.Content.ReadFromJsonAsync<List<PatientTaskViewModel>>(cancellationToken:t)??[];}
    public async Task<PatientTaskViewModel?>GetPatientTaskAsync(Guid p,Guid u,CancellationToken t=default){using var q=await Request(HttpMethod.Get,$"api/patients/{p}/tasks/{u}");using var r=await _client.SendAsync(q,t);if(r.StatusCode==HttpStatusCode.NotFound)return null;await Ensure(r);return await r.Content.ReadFromJsonAsync<PatientTaskViewModel>(cancellationToken:t);}
    public Task<PatientTaskViewModel?>CreatePatientTaskAsync(Guid p,object x,CancellationToken t)=>Mutate(HttpMethod.Post,$"api/patients/{p}/tasks",x,t);
    public Task<PatientTaskViewModel?>UpdatePatientTaskAsync(Guid p,Guid u,object x,CancellationToken t)=>Mutate(HttpMethod.Put,$"api/patients/{p}/tasks/{u}",x,t);
    public Task<PatientTaskViewModel?>CompletePatientTaskAsync(Guid p,Guid u,object x,CancellationToken t)=>Mutate(HttpMethod.Post,$"api/patients/{p}/tasks/{u}/complete",x,t);
    public Task<PatientTaskViewModel?>ReopenPatientTaskAsync(Guid p,Guid u,CancellationToken t)=>Mutate(HttpMethod.Post,$"api/patients/{p}/tasks/{u}/reopen",new{},t);
    public async Task<IReadOnlyList<PatientDashboardTaskViewModel>>GetDashboardOpenTasksAsync(int maxRows,CancellationToken t=default){using var q=await Request(HttpMethod.Get,$"api/patient-tasks/open?maxRows={maxRows}");using var r=await _client.SendAsync(q,t);await Ensure(r);return await r.Content.ReadFromJsonAsync<List<PatientDashboardTaskViewModel>>(cancellationToken:t)??[];}
    public async Task<int>GetOverdueCountAsync(CancellationToken t=default){using var q=await Request(HttpMethod.Get,"api/patient-tasks/overdue/count");using var r=await _client.SendAsync(q,t);await Ensure(r);return(await r.Content.ReadFromJsonAsync<OverdueCountResponse>(cancellationToken:t))?.Count??throw new HttpRequestException("The overdue task count response was invalid.");}
    private async Task<PatientTaskViewModel?>Mutate(HttpMethod method,string uri,object body,CancellationToken t){using var q=await Request(method,uri);q.Content=JsonContent.Create(body);using var r=await _client.SendAsync(q,t);if(r.StatusCode==HttpStatusCode.NotFound)return null;await Ensure(r);return await r.Content.ReadFromJsonAsync<PatientTaskViewModel>(cancellationToken:t);}
    private static async Task Ensure(HttpResponseMessage response){if(response.IsSuccessStatusCode)return;var message=response.StatusCode switch{HttpStatusCode.BadRequest=>"The task information is invalid.",HttpStatusCode.Conflict=>"Completed tasks cannot be edited.",_=>"The task operation could not be completed."};throw new HttpRequestException(message,null,response.StatusCode);}
    private async Task<HttpRequestMessage>Request(HttpMethod method,string uri){var request=new HttpRequestMessage(method,uri);await AddBearerTokenAsync(request);return request;}
    private async Task AddBearerTokenAsync(HttpRequestMessage request){var token=await(_context.HttpContext??throw new InvalidOperationException()).GetTokenAsync("access_token");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);}
    private sealed class OverdueCountResponse{public int Count{get;set;}}
}
