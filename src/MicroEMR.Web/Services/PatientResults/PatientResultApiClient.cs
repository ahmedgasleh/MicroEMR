using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Web.Models.PatientResults;

namespace MicroEMR.Web.Services.PatientResults;

public interface IPatientResultApiClient
{
    Task<int> GetUnreviewedCount(CancellationToken token=default);
    Task<IReadOnlyList<UnreviewedPatientResultViewModel>> ListUnreviewed(CancellationToken token=default);
    Task<IReadOnlyList<PatientResultResponse>> List(Guid patientUid,string status,CancellationToken token);
    Task<PatientResultResponse?> Create(Guid patientUid,object request,CancellationToken token);
    Task<PatientResultResponse?> Update(Guid patientUid,Guid uid,object request,CancellationToken token);
    Task<PatientResultResponse?> Review(Guid patientUid,Guid uid,object request,CancellationToken token);
}

public sealed class PatientResultApiClient(HttpClient client,IHttpContextAccessor contextAccessor):IPatientResultApiClient
{
    public async Task<int> GetUnreviewedCount(CancellationToken token=default)
    {
        using var request=await Request(HttpMethod.Get,"api/results/unreviewed-count");
        using var response=await client.SendAsync(request,token);response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UnreviewedCountResponse>(cancellationToken:token))?.Count
            ?? throw new HttpRequestException("The unreviewed result count response was invalid.");
    }
    public async Task<IReadOnlyList<UnreviewedPatientResultViewModel>> ListUnreviewed(CancellationToken token=default)
    {using var request=await Request(HttpMethod.Get,"api/results/unreviewed");using var response=await client.SendAsync(request,token);response.EnsureSuccessStatusCode();return await response.Content.ReadFromJsonAsync<List<UnreviewedPatientResultViewModel>>(cancellationToken:token)??[];}
    public async Task<IReadOnlyList<PatientResultResponse>> List(Guid patientUid,string status,CancellationToken token)
    {using var request=await Request(HttpMethod.Get,$"api/patients/{patientUid}/results?status={Uri.EscapeDataString(status)}");using var response=await client.SendAsync(request,token);response.EnsureSuccessStatusCode();return await response.Content.ReadFromJsonAsync<List<PatientResultResponse>>(cancellationToken:token)??[];}
    public Task<PatientResultResponse?>Create(Guid patientUid,object value,CancellationToken token)=>Mutate(HttpMethod.Post,$"api/patients/{patientUid}/results",value,token);
    public Task<PatientResultResponse?>Update(Guid patientUid,Guid uid,object value,CancellationToken token)=>Mutate(HttpMethod.Put,$"api/patients/{patientUid}/results/{uid}",value,token);
    public Task<PatientResultResponse?>Review(Guid patientUid,Guid uid,object value,CancellationToken token)=>Mutate(HttpMethod.Post,$"api/patients/{patientUid}/results/{uid}/mark-reviewed",value,token);
    private async Task<PatientResultResponse?>Mutate(HttpMethod method,string uri,object value,CancellationToken token){using var request=await Request(method,uri);request.Content=JsonContent.Create(value);using var response=await client.SendAsync(request,token);if(response.StatusCode==System.Net.HttpStatusCode.Conflict)throw new HttpRequestException("The result changed or is no longer editable. Reload and try again.",null,response.StatusCode);response.EnsureSuccessStatusCode();return await response.Content.ReadFromJsonAsync<PatientResultResponse>(cancellationToken:token);}
    private async Task<HttpRequestMessage>Request(HttpMethod method,string uri){var request=new HttpRequestMessage(method,uri);var token=await(contextAccessor.HttpContext??throw new InvalidOperationException()).GetTokenAsync("access_token");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);return request;}
    private sealed class UnreviewedCountResponse{public int Count{get;set;}}
}
