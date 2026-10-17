using System.Net;using System.Net.Http.Json;using System.Text.Json;using Microsoft.AspNetCore.Authentication;using MicroEMR.Application.PatientFamilyHistory;
namespace MicroEMR.Web.Services.PatientFamilyHistory;
public sealed class PatientFamilyHistoryApiClient(HttpClient client,IHttpContextAccessor context):IPatientFamilyHistoryApiClient
{
 public async Task<PatientFamilyHistoryResponse?>Get(Guid p,Guid h,CancellationToken ct=default){using var q=await Req(HttpMethod.Get,$"api/patients/{p}/family-history/{h}");return await Send(q,ct);}
 public async Task<IReadOnlyList<PatientFamilyHistoryResponse>>List(Guid p,string s,CancellationToken ct=default){using var q=await Req(HttpMethod.Get,$"api/patients/{p}/family-history?status={Uri.EscapeDataString(s)}");using var r=await client.SendAsync(q,ct);await Ok(r);return await r.Content.ReadFromJsonAsync<List<PatientFamilyHistoryResponse>>(cancellationToken:ct)??[];}
 public async Task<PatientFamilyHistoryResponse>Create(Guid p,UpdatePatientFamilyHistoryRequest x,CancellationToken ct=default){using var q=await Req(HttpMethod.Post,$"api/patients/{p}/family-history");q.Content=JsonContent.Create(new{x.Relationship,x.ConditionDescription,x.AgeAtOnset,x.LifeStageCode,x.Treatment,x.Notes});using var r=await client.SendAsync(q,ct);await Ok(r);return(await r.Content.ReadFromJsonAsync<PatientFamilyHistoryResponse>(cancellationToken:ct))!;}
 public async Task<PatientFamilyHistoryResponse?>Update(Guid p,Guid h,UpdatePatientFamilyHistoryRequest x,CancellationToken ct=default){using var q=await Req(HttpMethod.Put,$"api/patients/{p}/family-history/{h}");q.Content=JsonContent.Create(new{x.Relationship,x.ConditionDescription,x.AgeAtOnset,x.LifeStageCode,x.Treatment,x.Notes,x.RowVersion});return await Send(q,ct);}
 public async Task<PatientFamilyHistoryResponse?>Archive(Guid p,Guid h,string v,CancellationToken ct=default){using var q=await Req(HttpMethod.Post,$"api/patients/{p}/family-history/{h}/archive");q.Content=JsonContent.Create(new{rowVersion=v});return await Send(q,ct);}
 private async Task<PatientFamilyHistoryResponse?>Send(HttpRequestMessage q,CancellationToken ct){using var r=await client.SendAsync(q,ct);if(r.StatusCode==HttpStatusCode.NotFound)return null;await Ok(r);return await r.Content.ReadFromJsonAsync<PatientFamilyHistoryResponse>(cancellationToken:ct);}
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
   HttpStatusCode.BadRequest=>"The family medical history information is invalid.",
   HttpStatusCode.Unauthorized=>"Your session has expired. Sign in and try again.",
   HttpStatusCode.Forbidden=>"You are not authorized to change family medical history.",
   HttpStatusCode.NotFound=>"The patient or family medical history item was not found.",
   HttpStatusCode.Conflict=>"This history item was changed by another user.",
   HttpStatusCode.ServiceUnavailable=>"Family medical history is temporarily unavailable.",
   _=>"The family medical history operation failed."
  };
 }
}
