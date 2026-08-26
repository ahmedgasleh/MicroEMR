using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Application.Cdm;

namespace MicroEMR.Web.Services.Cdm;

public interface ICdmApiClient
{
    Task<CdmSummaryResponse> Summary(Guid patientUid,CancellationToken token);
    Task<CdmEnrollmentResponse> Enroll(Guid patientUid,CreateCdmEnrollmentRequest request,CancellationToken token);
    Task<CdmEnrollmentResponse> Inactivate(Guid patientUid,Guid enrollmentUid,InactivateCdmEnrollmentRequest request,CancellationToken token);
}
public sealed class CdmApiClient(HttpClient http,IHttpContextAccessor context):ICdmApiClient
{
    public async Task<CdmSummaryResponse> Summary(Guid p,CancellationToken t){using var q=await Request(HttpMethod.Get,$"api/patients/{p}/cdm");using var r=await http.SendAsync(q,t);r.EnsureSuccessStatusCode();return(await r.Content.ReadFromJsonAsync<CdmSummaryResponse>(cancellationToken:t))!;}
    public Task<CdmEnrollmentResponse> Enroll(Guid p,CreateCdmEnrollmentRequest x,CancellationToken t)=>Mutate(HttpMethod.Post,$"api/patients/{p}/cdm/enrollments",x,t);
    public Task<CdmEnrollmentResponse> Inactivate(Guid p,Guid e,InactivateCdmEnrollmentRequest x,CancellationToken t)=>Mutate(HttpMethod.Post,$"api/patients/{p}/cdm/enrollments/{e}/inactivate",x,t);
    private async Task<CdmEnrollmentResponse> Mutate(HttpMethod m,string u,object x,CancellationToken t){using var q=await Request(m,u);q.Content=JsonContent.Create(x);using var r=await http.SendAsync(q,t);r.EnsureSuccessStatusCode();return(await r.Content.ReadFromJsonAsync<CdmEnrollmentResponse>(cancellationToken:t))!;}
    private async Task<HttpRequestMessage> Request(HttpMethod m,string u){var q=new HttpRequestMessage(m,u);var token=await(context.HttpContext??throw new InvalidOperationException()).GetTokenAsync("access_token");q.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);return q;}
}
