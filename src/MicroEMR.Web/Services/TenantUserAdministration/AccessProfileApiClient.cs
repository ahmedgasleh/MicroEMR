using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Application.AccessProfiles;

namespace MicroEMR.Web.Services.TenantUserAdministration;
public interface IAccessProfileApiClient
{
 Task<IReadOnlyList<AccessProfileSummary>> ListAsync(CancellationToken token=default); Task<AccessProfileDetails?> GetAsync(Guid uid,CancellationToken token=default); Task<IReadOnlyList<BusinessPermission>> PermissionsAsync(CancellationToken token=default); Task UpdateAsync(Guid uid,IReadOnlyCollection<string> keys,string version,CancellationToken token=default); Task AssignAsync(string user,Guid uid,string version,CancellationToken token=default);
 Task<IReadOnlySet<string>> EffectivePermissionsAsync(CancellationToken token=default);
}
public sealed class AccessProfileApiClient(HttpClient client,IHttpContextAccessor accessor):IAccessProfileApiClient
{
 public async Task<IReadOnlyList<AccessProfileSummary>> ListAsync(CancellationToken t=default)=>await Send<List<AccessProfileSummary>>(HttpMethod.Get,"api/admin/access-profiles",null,t)??[];
 public async Task<AccessProfileDetails?> GetAsync(Guid u,CancellationToken t=default)=>await Send<AccessProfileDetails>(HttpMethod.Get,$"api/admin/access-profiles/{u}",null,t);
 public async Task<IReadOnlyList<BusinessPermission>> PermissionsAsync(CancellationToken t=default)=>await Send<List<BusinessPermission>>(HttpMethod.Get,"api/admin/access-profiles/permissions",null,t)??[];
 public async Task UpdateAsync(Guid u,IReadOnlyCollection<string> k,string v,CancellationToken t=default)=>_ = await Send<object>(HttpMethod.Put,$"api/admin/access-profiles/{u}/permissions",new{permissionKeys=k,rowVersion=v},t);
 public async Task AssignAsync(string user,Guid u,string v,CancellationToken t=default)=>_ = await Send<object>(HttpMethod.Put,$"api/admin/access-profiles/users/{Uri.EscapeDataString(user)}",new{accessProfileUid=u,rowVersion=v},t);
 public async Task<IReadOnlySet<string>> EffectivePermissionsAsync(CancellationToken t=default)=>(await Send<HashSet<string>>(HttpMethod.Get,"api/permissions/me",null,t))??new HashSet<string>(StringComparer.Ordinal);
 private async Task<T?> Send<T>(HttpMethod method,string uri,object? body,CancellationToken token){var context=accessor.HttpContext??throw new InvalidOperationException();var access=await context.GetTokenAsync("access_token")??throw new UnauthorizedAccessException();using var request=new HttpRequestMessage(method,uri);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",access);if(body is not null)request.Content=JsonContent.Create(body);using var response=await client.SendAsync(request,token);if(response.StatusCode==System.Net.HttpStatusCode.NotFound)return default;if(!response.IsSuccessStatusCode)throw new HttpRequestException("The access profile operation could not be completed.",null,response.StatusCode);if(response.StatusCode==System.Net.HttpStatusCode.NoContent)return default;return await response.Content.ReadFromJsonAsync<T>(cancellationToken:token);}
}
