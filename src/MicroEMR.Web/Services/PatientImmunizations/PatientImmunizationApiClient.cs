using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Web.Models.PatientImmunizations;

namespace MicroEMR.Web.Services.PatientImmunizations;

public interface IPatientImmunizationApiClient
{
    Task<IReadOnlyList<PatientImmunizationViewModel>> ListAsync(Guid patientUid,string status,CancellationToken token=default);
    Task<PatientImmunizationViewModel> CreateAsync(Guid patientUid,SavePatientImmunizationViewModel model,CancellationToken token=default);
    Task<PatientImmunizationViewModel?> UpdateAsync(Guid patientUid,Guid uid,SavePatientImmunizationViewModel model,CancellationToken token=default);
    Task<PatientImmunizationViewModel?> MarkEnteredInErrorAsync(Guid patientUid,Guid uid,MarkImmunizationEnteredInErrorViewModel model,CancellationToken token=default);
}

public sealed class PatientImmunizationApiClient(HttpClient client,IHttpContextAccessor context):IPatientImmunizationApiClient
{
    public async Task<IReadOnlyList<PatientImmunizationViewModel>> ListAsync(Guid p,string status,CancellationToken token=default)
    {using var q=await Request(HttpMethod.Get,$"api/patients/{p}/immunizations?status={Uri.EscapeDataString(status)}");using var r=await client.SendAsync(q,token);await Ensure(r);return await r.Content.ReadFromJsonAsync<List<PatientImmunizationViewModel>>(cancellationToken:token)??[];}
    public async Task<PatientImmunizationViewModel> CreateAsync(Guid p,SavePatientImmunizationViewModel m,CancellationToken token=default)
    {using var q=await Request(HttpMethod.Post,$"api/patients/{p}/immunizations");q.Content=JsonContent.Create(Payload(m,false));using var r=await client.SendAsync(q,token);await Ensure(r);return(await r.Content.ReadFromJsonAsync<PatientImmunizationViewModel>(cancellationToken:token))!;}
    public Task<PatientImmunizationViewModel?> UpdateAsync(Guid p,Guid uid,SavePatientImmunizationViewModel m,CancellationToken token=default)=>Send(HttpMethod.Put,$"api/patients/{p}/immunizations/{uid}",Payload(m,true),token);
    public Task<PatientImmunizationViewModel?> MarkEnteredInErrorAsync(Guid p,Guid uid,MarkImmunizationEnteredInErrorViewModel m,CancellationToken token=default)=>Send(HttpMethod.Post,$"api/patients/{p}/immunizations/{uid}/entered-in-error",new{m.Reason,m.RowVersion},token);
    private static object Payload(SavePatientImmunizationViewModel m,bool update)=>new{m.VaccineName,m.AdministrationDate,m.DoseNumber,m.Route,m.Site,m.LotNumber,m.SourceType,m.SourceDescription,m.AdministeredByName,m.EncounterUid,m.Notes,RowVersion=update?m.RowVersion:null};
    private async Task<PatientImmunizationViewModel?> Send(HttpMethod method,string url,object value,CancellationToken token){using var q=await Request(method,url);q.Content=JsonContent.Create(value);using var r=await client.SendAsync(q,token);if(r.StatusCode==HttpStatusCode.NotFound)return null;await Ensure(r);return await r.Content.ReadFromJsonAsync<PatientImmunizationViewModel>(cancellationToken:token);}
    private async Task<HttpRequestMessage> Request(HttpMethod method,string url){var token=await context.HttpContext!.GetTokenAsync("access_token");if(string.IsNullOrWhiteSpace(token))throw new UnauthorizedAccessException();var q=new HttpRequestMessage(method,url);q.Headers.Authorization=new("Bearer",token);return q;}
    private static async Task Ensure(HttpResponseMessage response){if(response.IsSuccessStatusCode)return;var message="The immunization operation failed.";try{using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());if(json.RootElement.TryGetProperty("message",out var p)&&p.ValueKind==JsonValueKind.String)message=p.GetString()!;}catch(JsonException){}throw new HttpRequestException(message,null,response.StatusCode);}
}
