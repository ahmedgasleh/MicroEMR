using System.Net;
using System.Net.Http.Json;
using MicroEMR.Application.PatientPrescriptions;

namespace MicroEMR.Web.Services.PatientPrescriptions;

public sealed class DispensingLocationApiClient(HttpClient http)
{
    public async Task<DispensingLocationPage> Search(string? query,bool maintenance,bool inactive,int page,CancellationToken token)=>await Send<DispensingLocationPage>(HttpMethod.Get,"api/dispensing-locations"+(maintenance?"/maintenance":"")+"?query="+Uri.EscapeDataString(query??"")+"&includeInactive="+inactive+"&page="+page,null,token)??new([],page,20,false);
    public Task<DispensingLocation?> Get(Guid uid,bool maintenance,CancellationToken token)=>Send<DispensingLocation>(HttpMethod.Get,"api/dispensing-locations/"+(maintenance?"maintenance/":"")+uid,null,token);
    public Task<DispensingLocation?> Save(Guid? uid,string mode,SaveDispensingLocation request,CancellationToken token)=>Send<DispensingLocation>(mode=="Edit"?HttpMethod.Put:HttpMethod.Post,"api/dispensing-locations"+(uid.HasValue?"/"+uid:"")+(mode is "Deactivate" or "Reactivate"?"/"+mode.ToLowerInvariant():""),request,token);
    private async Task<T?> Send<T>(HttpMethod method,string path,object? body,CancellationToken token)
    {
        using var request=new HttpRequestMessage(method,path);if(body is not null)request.Content=JsonContent.Create(body);
        using var response=await http.SendAsync(request,token);if(response.StatusCode==HttpStatusCode.NotFound)return default;
        if(!response.IsSuccessStatusCode)throw new HttpRequestException(response.StatusCode==HttpStatusCode.Conflict?"Location changed or identity is already owned. Reload and try again.":"Location request was rejected. Check the supplied fields and your permissions.",null,response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken:token);
    }
}
