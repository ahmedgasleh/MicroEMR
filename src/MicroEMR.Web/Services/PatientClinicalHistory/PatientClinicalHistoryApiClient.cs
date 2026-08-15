using System.Net;using System.Net.Http.Json;using System.Text.Json;using Microsoft.AspNetCore.Authentication;using MicroEMR.Web.Models.PatientClinicalHistory;
namespace MicroEMR.Web.Services.PatientClinicalHistory;
public sealed class PatientClinicalHistoryApiClient(HttpClient client,IHttpContextAccessor context):IPatientClinicalHistoryApiClient
{
 public async Task<IReadOnlyList<PatientClinicalHistoryViewModel>>List(Guid p,string s,CancellationToken ct=default){using var q=await Req(HttpMethod.Get,$"api/patients/{p}/clinical-history?status={Uri.EscapeDataString(s)}");using var r=await client.SendAsync(q,ct);await Ok(r);return await r.Content.ReadFromJsonAsync<List<PatientClinicalHistoryViewModel>>(cancellationToken:ct)??[];}
 public async Task<PatientClinicalHistoryViewModel>Create(Guid p,SavePatientClinicalHistoryViewModel x,CancellationToken ct=default){using var q=await Req(HttpMethod.Post,$"api/patients/{p}/clinical-history");q.Content=JsonContent.Create(new{x.HistoryType,x.Description,x.RelevantDate});using var r=await client.SendAsync(q,ct);await Ok(r);return(await r.Content.ReadFromJsonAsync<PatientClinicalHistoryViewModel>(cancellationToken:ct))!;}
 public async Task<PatientClinicalHistoryViewModel?>Update(Guid p,Guid h,SavePatientClinicalHistoryViewModel x,CancellationToken ct=default){using var q=await Req(HttpMethod.Put,$"api/patients/{p}/clinical-history/{h}");q.Content=JsonContent.Create(new{x.HistoryType,x.Description,x.RelevantDate,x.RowVersion});return await Send(q,ct);}
 public async Task<PatientClinicalHistoryViewModel?>Archive(Guid p,Guid h,string v,CancellationToken ct=default){using var q=await Req(HttpMethod.Post,$"api/patients/{p}/clinical-history/{h}/archive");q.Content=JsonContent.Create(new{rowVersion=v});return await Send(q,ct);}
 private async Task<PatientClinicalHistoryViewModel?>Send(HttpRequestMessage q,CancellationToken ct){using var r=await client.SendAsync(q,ct);if(r.StatusCode==HttpStatusCode.NotFound)return null;await Ok(r);return await r.Content.ReadFromJsonAsync<PatientClinicalHistoryViewModel>(cancellationToken:ct);}
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
   HttpStatusCode.BadRequest=>"The clinical history information is invalid.",
   HttpStatusCode.Unauthorized=>"Your session has expired. Sign in and try again.",
   HttpStatusCode.Forbidden=>"You are not authorized to change clinical history.",
   HttpStatusCode.NotFound=>"The patient or clinical history item was not found.",
   HttpStatusCode.Conflict=>"This history item was changed by another user.",
   HttpStatusCode.ServiceUnavailable=>"Clinical history is temporarily unavailable.",
   _=>"The clinical history operation failed."
  };
 }
}
