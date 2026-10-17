using System.Net;using System.Net.Http.Json;using System.Text.Json;using Microsoft.AspNetCore.Authentication;using MicroEMR.Application.PatientRiskFactors;
namespace MicroEMR.Web.Services.PatientRiskFactors;
public sealed class PatientRiskFactorApiClient(HttpClient client,IHttpContextAccessor context):IPatientRiskFactorApiClient
{
 public async Task<PatientRiskFactorResponse?>Get(Guid p,Guid h,CancellationToken ct=default){using var q=await Req(HttpMethod.Get,$"api/patients/{p}/risk-factors/{h}");return await Send(q,ct);}
 public async Task<IReadOnlyList<PatientRiskFactorResponse>>List(Guid p,string s,CancellationToken ct=default){using var q=await Req(HttpMethod.Get,$"api/patients/{p}/risk-factors?status={Uri.EscapeDataString(s)}");using var r=await client.SendAsync(q,ct);await Ok(r);return await r.Content.ReadFromJsonAsync<List<PatientRiskFactorResponse>>(cancellationToken:ct)??[];}
 public async Task<PatientRiskFactorResponse>Create(Guid p,UpdatePatientRiskFactorRequest x,CancellationToken ct=default){using var q=await Req(HttpMethod.Post,$"api/patients/{p}/risk-factors");q.Content=JsonContent.Create(new{x.RiskFactor,x.ExposureDetails,x.LifeStageCode,x.Notes});using var r=await client.SendAsync(q,ct);await Ok(r);return(await r.Content.ReadFromJsonAsync<PatientRiskFactorResponse>(cancellationToken:ct))!;}
 public async Task<PatientRiskFactorResponse?>Update(Guid p,Guid h,UpdatePatientRiskFactorRequest x,CancellationToken ct=default){using var q=await Req(HttpMethod.Put,$"api/patients/{p}/risk-factors/{h}");q.Content=JsonContent.Create(new{x.RiskFactor,x.ExposureDetails,x.LifeStageCode,x.Notes,x.RowVersion});return await Send(q,ct);}
 public async Task<PatientRiskFactorResponse?>Archive(Guid p,Guid h,string v,CancellationToken ct=default){using var q=await Req(HttpMethod.Post,$"api/patients/{p}/risk-factors/{h}/archive");q.Content=JsonContent.Create(new{rowVersion=v});return await Send(q,ct);}
 private async Task<PatientRiskFactorResponse?>Send(HttpRequestMessage q,CancellationToken ct){using var r=await client.SendAsync(q,ct);if(r.StatusCode==HttpStatusCode.NotFound)return null;await Ok(r);return await r.Content.ReadFromJsonAsync<PatientRiskFactorResponse>(cancellationToken:ct);}
 private async Task<HttpRequestMessage>Req(HttpMethod m,string u){var token=await context.HttpContext!.GetTokenAsync("access_token");if(string.IsNullOrWhiteSpace(token))throw new UnauthorizedAccessException();var q=new HttpRequestMessage(m,u);q.Headers.Authorization=new("Bearer",token);return q;}
 private static async Task Ok(HttpResponseMessage r)
 {
  if(r.IsSuccessStatusCode)return;
  var message=await ErrorMessage(r);
  throw new HttpRequestException(message,null,r.StatusCode);
 }
 private static async Task<string> ErrorMessage(HttpResponseMessage r)
 {
  try
  {
   var body=await r.Content.ReadAsStringAsync();
   if(!string.IsNullOrWhiteSpace(body))
   {
    using var json=JsonDocument.Parse(body);
    foreach(var property in new[]{"detail","message","title"})
     if(json.RootElement.TryGetProperty(property,out var value)&&value.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(value.GetString()))
      return value.GetString()!;
   }
  }
  catch(JsonException) { }
  return r.StatusCode switch
  {
   HttpStatusCode.BadRequest=>"The risk factors information is invalid.",
   HttpStatusCode.Unauthorized=>"Your session has expired. Sign in and try again.",
   HttpStatusCode.Forbidden=>"You are not authorized to change risk factors.",
   HttpStatusCode.NotFound=>"The patient or risk factors item was not found.",
   HttpStatusCode.Conflict=>"This history item was changed by another user.",
   HttpStatusCode.ServiceUnavailable=>"Risk factors is temporarily unavailable.",
   _=>"The risk factors operation failed."
  };
 }
}
