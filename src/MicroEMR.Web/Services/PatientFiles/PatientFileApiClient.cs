using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Web.Models.PatientFiles;

namespace MicroEMR.Web.Services.PatientFiles;

public sealed class PatientFileApiClient(HttpClient client, IHttpContextAccessor contextAccessor) : IPatientFileApiClient
{
    public async Task<IReadOnlyList<PatientFileViewModel>> GetByPatientUidAsync(Guid patientUid, CancellationToken cancellationToken = default)
    {
        using var request = await RequestAsync(HttpMethod.Get, $"api/patients/{patientUid}/files");
        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<PatientFileViewModel>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<PatientFileViewModel?> GetByUidAsync(Guid patientUid, Guid fileUid, CancellationToken cancellationToken = default)
    {
        using var request = await RequestAsync(HttpMethod.Get, $"api/patients/{patientUid}/files/{fileUid}");
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PatientFileViewModel>(cancellationToken: cancellationToken);
    }

    public async Task<PatientFileViewModel?> UploadAsync(Guid patientUid, IFormFile file, UploadPatientFileViewModel metadata, CancellationToken cancellationToken = default)
    {
        using var request = await RequestAsync(HttpMethod.Post, $"api/patients/{patientUid}/files");
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(file.OpenReadStream());
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
        content.Add(streamContent, "file", file.FileName);
        Add(content, "description", metadata.Description);
        Add(content, "category", metadata.Category);
        Add(content, "title", metadata.Title);
        Add(content, "sourceOrganization", metadata.SourceOrganization);
        Add(content, "authorName", metadata.AuthorName);
        if (metadata.DocumentDate.HasValue) content.Add(new StringContent(metadata.DocumentDate.Value.ToString("yyyy-MM-dd")), "documentDate");
        if (metadata.ReceivedDate.HasValue) content.Add(new StringContent(metadata.ReceivedDate.Value.ToString("yyyy-MM-dd")), "receivedDate");
        request.Content = content;
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PatientFileViewModel>(cancellationToken: cancellationToken);
    }

    private static void Add(MultipartFormDataContent content, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) content.Add(new StringContent(value.Trim()), name);
    }

    public async Task<HttpResponseMessage> GetContentAsync(Guid patientUid, Guid fileUid, CancellationToken cancellationToken = default)
    {
        using var request = await RequestAsync(HttpMethod.Get, $"api/patients/{patientUid}/files/{fileUid}/content");
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return response;
        try { await EnsureSuccessAsync(response, cancellationToken); return response; }
        catch { response.Dispose(); throw; }
    }
    public Task<PatientFileViewModel?> ArchiveAsync(Guid p,Guid f,string v,CancellationToken ct=default)=>TransitionAsync(p,f,"archive",v,ct);
    public Task<PatientFileViewModel?> RestoreAsync(Guid p,Guid f,string v,CancellationToken ct=default)=>TransitionAsync(p,f,"restore",v,ct);
    private async Task<PatientFileViewModel?> TransitionAsync(Guid p,Guid f,string action,string v,CancellationToken ct)
    {using var request=await RequestAsync(HttpMethod.Post,$"api/patients/{p}/files/{f}/{action}");request.Content=JsonContent.Create(new{rowVersion=v});using var response=await client.SendAsync(request,ct);if(response.StatusCode==HttpStatusCode.NotFound)return null;await EnsureSuccessAsync(response,ct);return await response.Content.ReadFromJsonAsync<PatientFileViewModel>(cancellationToken:ct);}

    private async Task<HttpRequestMessage> RequestAsync(HttpMethod method, string uri)
    {
        var context = contextAccessor.HttpContext ?? throw new InvalidOperationException("The authenticated request context is unavailable.");
        var token = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedAccessException("The API access token is unavailable.");
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => await ReadValidationMessageAsync(response, cancellationToken),
            HttpStatusCode.Unauthorized => "Your session is no longer authorized.",
            HttpStatusCode.Forbidden => "You are not authorized to perform this file operation.",
            HttpStatusCode.NotFound => "The patient or file was not found.",
            HttpStatusCode.Conflict => "This file was changed by another user. The latest information has been reloaded.",
            _ => "The file operation could not be completed."
        };
        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private static async Task<string> ReadValidationMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (json.RootElement.TryGetProperty("errors", out var errors))
                foreach (var property in errors.EnumerateObject())
                    if (property.Value.ValueKind == JsonValueKind.Array && property.Value.GetArrayLength() > 0)
                        return property.Value[0].GetString() ?? "Please correct the file information.";
        }
        catch (JsonException) { }
        return "Please correct the file information.";
    }
}
