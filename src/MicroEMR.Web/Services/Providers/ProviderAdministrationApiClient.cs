using System.Net;
using System.Net.Http.Json;
using MicroEMR.Application.Providers;

namespace MicroEMR.Web.Services.Providers;

public interface IProviderAdministrationApiClient
{
 Task<IReadOnlyList<ProviderAdministrationItem>>List(string status,CancellationToken token=default);Task<ProviderAdministrationItem?>Get(Guid uid,CancellationToken token=default);Task<ProviderAdministrationItem>Create(SaveProviderRequest x,CancellationToken token=default);Task<ProviderAdministrationItem?>Update(Guid uid,SaveProviderRequest x,CancellationToken token=default);Task<ProviderAdministrationItem?>SetActive(Guid uid,bool active,string version,CancellationToken token=default);Task<IReadOnlyList<EligibleApplicationUser>>Eligible(Guid uid,CancellationToken token=default);Task<ProviderAdministrationItem?>Link(Guid uid,ProviderLinkRequest x,bool unlink,CancellationToken token=default);
}
public sealed class ProviderAdministrationApiClient(HttpClient client):IProviderAdministrationApiClient
{
 const string Root="api/providers";
 public async Task<IReadOnlyList<ProviderAdministrationItem>>List(string status,CancellationToken token=default)=>await client.GetFromJsonAsync<ProviderAdministrationItem[]>($"{Root}?status={Uri.EscapeDataString(status)}",token)??[];
 public async Task<ProviderAdministrationItem?>Get(Guid uid,CancellationToken token=default){var r=await client.GetAsync($"{Root}/{uid}",token);if(r.StatusCode==HttpStatusCode.NotFound)return null;await Ensure(r);return await r.Content.ReadFromJsonAsync<ProviderAdministrationItem>(cancellationToken:token);}
 public async Task<ProviderAdministrationItem>Create(SaveProviderRequest x,CancellationToken token=default){var r=await client.PostAsJsonAsync(Root,x,token);await Ensure(r);return(await r.Content.ReadFromJsonAsync<ProviderAdministrationItem>(cancellationToken:token))!;}
 public async Task<ProviderAdministrationItem?>Update(Guid uid,SaveProviderRequest x,CancellationToken token=default)=>await Send(HttpMethod.Put,$"{Root}/{uid}",x,token);
 public async Task<ProviderAdministrationItem?>SetActive(Guid uid,bool active,string version,CancellationToken token=default)=>await Send(HttpMethod.Post,$"{Root}/{uid}/{(active?"reactivate":"deactivate")}",new ProviderVersionRequest(version),token);
 public async Task<IReadOnlyList<EligibleApplicationUser>>Eligible(Guid uid,CancellationToken token=default)=>await client.GetFromJsonAsync<EligibleApplicationUser[]>($"{Root}/eligible-users?providerUid={uid}",token)??[];
 public Task<ProviderAdministrationItem?>Link(Guid uid,ProviderLinkRequest x,bool unlink,CancellationToken token=default)=>Send(unlink?HttpMethod.Delete:HttpMethod.Post,$"{Root}/{uid}/link-user",x,token);
 async Task<ProviderAdministrationItem?>Send(HttpMethod method,string url,object x,CancellationToken token){using var q=new HttpRequestMessage(method,url){Content=JsonContent.Create(x)};using var r=await client.SendAsync(q,token);if(r.StatusCode==HttpStatusCode.NotFound)return null;await Ensure(r);return await r.Content.ReadFromJsonAsync<ProviderAdministrationItem>(cancellationToken:token);}
 static async Task Ensure(HttpResponseMessage r){if(r.IsSuccessStatusCode)return;var message=r.StatusCode switch{HttpStatusCode.Conflict=>"Provider changed or the requested association is no longer available.",HttpStatusCode.Forbidden=>"You are not authorized to manage providers.",HttpStatusCode.BadRequest=>"Provider information is invalid.",_=>"Provider operation failed."};throw new HttpRequestException(message,null,r.StatusCode);}
}
