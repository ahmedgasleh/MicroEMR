using System.Net;
using System.Net.Http.Json;
using MicroEMR.Application.Scheduling;

namespace MicroEMR.Web.Services.Scheduling;

public sealed class SchedulingResourceAdministrationApiClient(HttpClient client)
{
    private const string Root = "api/admin/scheduling-resources";
    public async Task<IReadOnlyList<SchedulingResourceItem>> ListAsync(string status, CancellationToken token) =>
        await Read<SchedulingResourceItem[]>($"{Root}?status={Uri.EscapeDataString(status)}", token) ?? [];
    public async Task<IReadOnlyList<SchedulingProviderChoice>> ProvidersAsync(CancellationToken token) =>
        await Read<SchedulingProviderChoice[]>($"{Root}/providers", token) ?? [];
    public Task<SchedulingResourceItem?> GetAsync(Guid uid, CancellationToken token) => Read<SchedulingResourceItem>($"{Root}/{uid}", token);
    public Task<SchedulingResourceItem?> SaveAsync(Guid? uid, SaveSchedulingResourceRequest request, CancellationToken token) =>
        Send(uid.HasValue ? HttpMethod.Put : HttpMethod.Post, uid.HasValue ? $"{Root}/{uid}" : Root, request, token);
    public Task<SchedulingResourceItem?> SetActiveAsync(Guid uid, bool active, string version, CancellationToken token) =>
        Send(HttpMethod.Post, $"{Root}/{uid}/{(active ? "activate" : "deactivate")}", new SchedulingResourceVersionRequest(version), token);
    private async Task<T?> Read<T>(string url, CancellationToken token)
    {
        using var response = await client.GetAsync(url, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return default;
        await Ensure(response, token);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: token);
    }
    private async Task<SchedulingResourceItem?> Send(HttpMethod method, string url, object body, CancellationToken token)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        using var response = await client.SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await Ensure(response, token);
        return await response.Content.ReadFromJsonAsync<SchedulingResourceItem>(cancellationToken: token);
    }
    private static async Task Ensure(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        var message = "Scheduling resource operation failed. Try again later.";
        // Only the bounded validation/conflict contract is suitable for display.
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            try { message = (await response.Content.ReadFromJsonAsync<ResourceError>(cancellationToken: token))?.Message ?? message; }
            catch (System.Text.Json.JsonException) { }
        }
        else if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized) message = "You are not authorized to manage scheduling resources.";
        throw new HttpRequestException(message, null, response.StatusCode);
    }
    private sealed record ResourceError(string Message);
}
