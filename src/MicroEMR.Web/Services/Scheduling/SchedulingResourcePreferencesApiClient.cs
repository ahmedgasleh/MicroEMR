using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Application.Scheduling.Preferences;

namespace MicroEMR.Web.Services.Scheduling;

public interface ISchedulingResourcePreferencesApiClient
{
    Task<SchedulingResourcePreferencesResponse> GetAsync(CancellationToken token = default);
    Task<SchedulingResourcePreferencesResponse> SaveAsync(SaveSchedulingResourcePreferencesRequest request, CancellationToken token = default);
}

public sealed class SchedulingResourcePreferencesApiClient(HttpClient http, IHttpContextAccessor contexts)
    : ISchedulingResourcePreferencesApiClient
{
    public Task<SchedulingResourcePreferencesResponse> GetAsync(CancellationToken token = default) => SendAsync(HttpMethod.Get, null, token);
    public Task<SchedulingResourcePreferencesResponse> SaveAsync(SaveSchedulingResourcePreferencesRequest request, CancellationToken token = default) => SendAsync(HttpMethod.Put, request, token);

    private async Task<SchedulingResourcePreferencesResponse> SendAsync(HttpMethod method,
        SaveSchedulingResourcePreferencesRequest? body, CancellationToken token)
    {
        var context = contexts.HttpContext ?? throw new InvalidOperationException("An HTTP context is required.");
        var accessToken = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(accessToken)) throw new UnauthorizedAccessException("The access token is missing.");
        using var request = new HttpRequestMessage(method, "api/scheduling/resource-preferences");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.Conflict) throw new SchedulingResourcePreferencesConcurrencyException();
        // Do not forward API error bodies or authentication redirects to preference feedback.
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SchedulingResourcePreferencesResponse>(cancellationToken: token)
            ?? throw new HttpRequestException("Resource defaults returned no response.");
    }
}
