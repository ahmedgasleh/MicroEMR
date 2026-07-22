using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Web.Models.PatientProblems;

namespace MicroEMR.Web.Services.PatientProblems;

public sealed class PatientProblemApiClient(HttpClient httpClient, IHttpContextAccessor contextAccessor, ILogger<PatientProblemApiClient> logger) : IPatientProblemApiClient
{
    public async Task<IReadOnlyList<PatientProblemViewModel>> GetByPatientUidAsync(Guid patientUid, string statusFilter, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/patients/{patientUid}/problems?status={Uri.EscapeDataString(statusFilter)}");
        await AddBearerTokenAsync(request);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<PatientProblemViewModel>>(cancellationToken: cancellationToken) ?? [];
    }
    public async Task<PatientProblemViewModel?> GetByUidAsync(Guid patientUid, Guid patientProblemUid, CancellationToken cancellationToken = default)
        => await SendOptionalAsync(HttpMethod.Get, $"api/patients/{patientUid}/problems/{patientProblemUid}", null, cancellationToken);
    public async Task<PatientProblemViewModel> CreateAsync(Guid patientUid, CreatePatientProblemRequest requestBody, CancellationToken cancellationToken = default)
        => await SendOptionalAsync(HttpMethod.Post, $"api/patients/{patientUid}/problems", JsonContent.Create(requestBody), cancellationToken)
            ?? throw new InvalidOperationException("The API created the problem but returned no problem data.");
    public Task<PatientProblemViewModel?> UpdateAsync(Guid patientUid, Guid patientProblemUid, UpdatePatientProblemRequest requestBody, CancellationToken cancellationToken = default)
        => SendOptionalAsync(HttpMethod.Put, $"api/patients/{patientUid}/problems/{patientProblemUid}", JsonContent.Create(requestBody), cancellationToken);
    public Task<PatientProblemViewModel?> ResolveAsync(Guid patientUid, Guid patientProblemUid, ResolvePatientProblemRequest requestBody, CancellationToken cancellationToken = default)
        => SendOptionalAsync(HttpMethod.Post, $"api/patients/{patientUid}/problems/{patientProblemUid}/resolve", JsonContent.Create(requestBody), cancellationToken);

    private async Task<PatientProblemViewModel?> SendOptionalAsync(HttpMethod method, string uri, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = content };
        await AddBearerTokenAsync(request);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PatientProblemViewModel>(cancellationToken: cancellationToken);
    }
    private async Task AddBearerTokenAsync(HttpRequestMessage request)
    {
        var context = contextAccessor.HttpContext ?? throw new InvalidOperationException("No active HTTP context is available.");
        var token = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedAccessException("The access token is missing. Sign in again.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("MicroEMR API problem request failed with status {StatusCode}.", (int)response.StatusCode);
        throw new HttpRequestException($"MicroEMR API request failed with status {(int)response.StatusCode}. {body}", null, response.StatusCode);
    }
}
