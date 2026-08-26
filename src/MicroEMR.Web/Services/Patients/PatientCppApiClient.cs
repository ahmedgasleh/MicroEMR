using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Application.PatientCpp;

namespace MicroEMR.Web.Services.Patients;

public interface IPatientCppApiClient
{
    Task<PatientCppSummaryResponse?> GetAsync(Guid patientUid, CancellationToken cancellationToken = default);
}

public sealed class PatientCppApiClient(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor) : IPatientCppApiClient
{
    public async Task<PatientCppSummaryResponse?> GetAsync(Guid patientUid, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/patients/{patientUid}/cpp");
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HTTP context is available.");
        var token = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedAccessException("The access token is missing.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("Patient summary access was denied.");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PatientCppSummaryResponse>(cancellationToken: cancellationToken);
    }
}

