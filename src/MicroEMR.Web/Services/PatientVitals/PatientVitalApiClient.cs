using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Web.Models.PatientVitals;
namespace MicroEMR.Web.Services.PatientVitals;
public sealed class PatientVitalApiClient(HttpClient httpClient,IHttpContextAccessor accessor,ILogger<PatientVitalApiClient> logger):IPatientVitalApiClient
{
 public async Task<IReadOnlyList<PatientVitalViewModel>> GetPatientVitalsAsync(Guid patientUid,CancellationToken ct=default){using var req=new HttpRequestMessage(HttpMethod.Get,$"api/patients/{patientUid}/vitals");await Token(req);using var res=await httpClient.SendAsync(req,ct);await Success(res,ct);return await res.Content.ReadFromJsonAsync<List<PatientVitalViewModel>>(cancellationToken:ct)??[];}
 public Task<PatientVitalViewModel?> GetPatientVitalAsync(Guid p,Guid v,CancellationToken ct=default)=>Send(HttpMethod.Get,$"api/patients/{p}/vitals/{v}",null,ct);
 public Task<PatientVitalViewModel?> CreatePatientVitalAsync(Guid p,CreatePatientVitalRequest body,CancellationToken ct=default)=>Send(HttpMethod.Post,$"api/patients/{p}/vitals",JsonContent.Create(body),ct);
 public Task<PatientVitalViewModel?> UpdatePatientVitalAsync(Guid p,Guid v,UpdatePatientVitalRequest body,CancellationToken ct=default)=>Send(HttpMethod.Put,$"api/patients/{p}/vitals/{v}",JsonContent.Create(body),ct);
 private async Task<PatientVitalViewModel?> Send(HttpMethod method,string uri,HttpContent? content,CancellationToken ct){using var req=new HttpRequestMessage(method,uri){Content=content};await Token(req);using var res=await httpClient.SendAsync(req,ct);if(res.StatusCode==HttpStatusCode.NotFound)return null;await Success(res,ct);return await res.Content.ReadFromJsonAsync<PatientVitalViewModel>(cancellationToken:ct);}
 private async Task Token(HttpRequestMessage req){var context=accessor.HttpContext??throw new InvalidOperationException("No active HTTP context is available.");var token=await context.GetTokenAsync("access_token");if(string.IsNullOrWhiteSpace(token))throw new UnauthorizedAccessException("The access token is missing.");req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);}
 private async Task Success(HttpResponseMessage response,CancellationToken ct){if(response.IsSuccessStatusCode)return;logger.LogWarning("MicroEMR API vitals request failed with status {StatusCode}.",(int)response.StatusCode);var body=await response.Content.ReadAsStringAsync(ct);throw new HttpRequestException(body,null,response.StatusCode);}
}
