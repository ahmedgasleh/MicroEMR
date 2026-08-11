using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Web.Models.TemplateAdministration;

namespace MicroEMR.Web.Services.TemplateAdministration;

public sealed class TemplateAdministrationApiClient(HttpClient http,IHttpContextAccessor contexts,ILogger<TemplateAdministrationApiClient> logger)
    : ITemplateAdministrationApiClient
{
    public Task<IReadOnlyList<TemplateDetailsModel>> ListAsync(string status,CancellationToken token=default)=>
        GetListAsync($"api/document-templates/administration?status={Uri.EscapeDataString(status)}",token);
    public Task<TemplateDetailsModel?> GetAsync(Guid uid,CancellationToken token=default)=>GetAsync<TemplateDetailsModel>($"api/document-templates/administration/{uid}",token);
    public Task<IReadOnlyList<TemplateVersionModel>> GetVersionsAsync(Guid uid,CancellationToken token=default)=>GetVersions($"api/document-templates/{uid}/versions",token);

    public Task<TemplateAdministrationResultModel?> CreateAsync(CreateTemplateViewModel model,CancellationToken token=default)=>
        SendAsync<TemplateAdministrationResultModel>(HttpMethod.Post,"api/document-templates/administration",new{name=model.Name,model.TemplateKind,model.Category,model.TemplateScope,definition=new TemplateDefinition{SchemaVersion=1,Sections=[]}},token);
    public Task<TemplateVersionModel?> OpenDraftAsync(Guid uid,CancellationToken token=default)=>
        SendAsync<TemplateVersionModel>(HttpMethod.Post,$"api/document-templates/{uid}/versions/draft",null,token);
    public Task<TemplateVersionModel?> SaveDraftAsync(SaveTemplateDefinitionViewModel model,CancellationToken token=default)=>
        SendAsync<TemplateVersionModel>(HttpMethod.Put,$"api/document-templates/{model.TemplateUid}/versions/{model.TemplateVersionUid}",new{templateContent=model.TemplateContent,schemaVersion=1,definitionJson=JsonSerializer.Serialize(model.Definition,JsonOptions),rowVersion=model.RowVersion},token);
    public async Task<TemplateValidationResponseModel> ValidateAsync(TemplateDefinition definition,CancellationToken token=default)=>
        await SendAsync<TemplateValidationResponseModel>(HttpMethod.Post,"api/document-templates/definition/validate",definition,token,true) ?? new();
    public Task<TemplateVersionModel?> PublishAsync(PublishTemplateViewModel model,CancellationToken token=default)=>
        SendAsync<TemplateVersionModel>(HttpMethod.Post,$"api/document-templates/{model.TemplateUid}/versions/{model.TemplateVersionUid}/publish",new{model.RowVersion},token);
    public Task<TemplateAdministrationResultModel?> CloneAsync(CloneTemplateViewModel model,CancellationToken token=default)=>
        SendAsync<TemplateAdministrationResultModel>(HttpMethod.Post,$"api/document-templates/administration/{model.TemplateUid}/clone",new{model.Name,model.TemplateScope},token);
    public Task<TemplateDetailsModel?> SetActiveAsync(SetTemplateActiveViewModel model,CancellationToken token=default)=>
        SendAsync<TemplateDetailsModel>(HttpMethod.Post,$"api/document-templates/administration/{model.TemplateUid}/set-active",new{model.IsActive,model.RowVersion},token);
    public Task<TemplateDetailsModel?> UpdateMetadataAsync(UpdateTemplateMetadataViewModel model,CancellationToken token=default)=>
        SendAsync<TemplateDetailsModel>(HttpMethod.Put,$"api/document-templates/administration/{model.TemplateUid}/metadata",new{model.Name,model.TemplateKind,model.Category,model.TemplateScope,model.OwnerUserId,model.RowVersion},token);

    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);
    private async Task<IReadOnlyList<TemplateDetailsModel>> GetListAsync(string uri,CancellationToken token)=>await GetAsync<List<TemplateDetailsModel>>(uri,token)??[];
    private async Task<IReadOnlyList<TemplateVersionModel>> GetVersions(string uri,CancellationToken token)=>await GetAsync<List<TemplateVersionModel>>(uri,token)??[];
    private async Task<T?> GetAsync<T>(string uri,CancellationToken token)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,uri);await Authorize(request);using var response=await http.SendAsync(request,token);
        if(response.StatusCode==HttpStatusCode.NotFound)return default;return await Read<T>(response,token);
    }
    private async Task<T?> SendAsync<T>(HttpMethod method,string uri,object? body,CancellationToken token,bool allowValidationFailure=false)
    {
        using var request=new HttpRequestMessage(method,uri);if(body is not null)request.Content=JsonContent.Create(body,options:JsonOptions);await Authorize(request);
        using var response=await http.SendAsync(request,token);if(response.StatusCode==HttpStatusCode.NotFound)return default;
        if(allowValidationFailure&&response.StatusCode==HttpStatusCode.BadRequest)return await response.Content.ReadFromJsonAsync<T>(JsonOptions,token);
        return await Read<T>(response,token);
    }
    private async Task<T?> Read<T>(HttpResponseMessage response,CancellationToken token)
    {
        if(response.IsSuccessStatusCode)return await response.Content.ReadFromJsonAsync<T>(JsonOptions,token);
        var body=await response.Content.ReadAsStringAsync(token);logger.LogWarning("Template administration API failed with {StatusCode}: {ResponseBody}",(int)response.StatusCode,body);
        throw new TemplateAdministrationApiException((int)response.StatusCode,body);
    }
    private async Task Authorize(HttpRequestMessage request)
    {
        var context=contexts.HttpContext??throw new InvalidOperationException("No active HTTP context is available.");
        var token=await context.GetTokenAsync("access_token");if(string.IsNullOrWhiteSpace(token))throw new UnauthorizedAccessException("The access token is missing. Sign in again.");
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
    }
}
