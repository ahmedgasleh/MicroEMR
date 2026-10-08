using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Application.PatientCpp;

namespace MicroEMR.Web.Services.Patients;

public interface ICppDisplayPreferencesApiClient
{
    Task<CppDisplayPreferencesResponse> GetAsync(CancellationToken cancellationToken = default);
    Task<CppDisplayPreferencesResponse> SaveAsync(SaveCppDisplayPreferencesRequest request, CancellationToken cancellationToken = default);
}

public sealed class CppDisplayPreferencesApiClient(HttpClient http, IHttpContextAccessor contexts) : ICppDisplayPreferencesApiClient
{
    public Task<CppDisplayPreferencesResponse> GetAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, null, cancellationToken);

    public Task<CppDisplayPreferencesResponse> SaveAsync(SaveCppDisplayPreferencesRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, request, cancellationToken);

    private async Task<CppDisplayPreferencesResponse> SendAsync(HttpMethod method, SaveCppDisplayPreferencesRequest? body, CancellationToken cancellationToken)
    {
        var context = contexts.HttpContext ?? throw new InvalidOperationException("An HTTP context is required.");
        var token = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedAccessException("The access token is missing.");
        using var request = new HttpRequestMessage(method, "api/cpp/display-preferences");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("CPP display preference access was denied.");
        if (response.StatusCode == HttpStatusCode.Conflict) throw new CppDisplayPreferencesConcurrencyException();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CppDisplayPreferencesResponse>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("CPP display preferences returned no response.");
    }
}
