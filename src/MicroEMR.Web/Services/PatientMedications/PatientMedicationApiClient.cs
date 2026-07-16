using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Web.Models.PatientMedications;

namespace MicroEMR.Web.Services.PatientMedications;

public sealed class PatientMedicationApiClient : IPatientMedicationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PatientMedicationApiClient> _logger;

    public PatientMedicationApiClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PatientMedicationApiClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PatientMedicationListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/patients/{patientUid}/medications");

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var medications =
            await response.Content.ReadFromJsonAsync<
                List<PatientMedicationListItemResponse>>(
                cancellationToken: cancellationToken);

        return medications ?? [];
    }

    public async Task<PatientMedicationDetailsResponse?> GetByUidAsync(
        Guid medicationUid,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/patient-medications/{medicationUid}");

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content
            .ReadFromJsonAsync<PatientMedicationDetailsResponse>(
                cancellationToken: cancellationToken);
    }

    public async Task<PatientMedicationDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientMedicationRequest medicationRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/patients/{patientUid}/medications")
        {
            Content = JsonContent.Create(medicationRequest)
        };

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content
                   .ReadFromJsonAsync<PatientMedicationDetailsResponse>(
                       cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException(
                   "The API created the medication but returned no medication data.");
    }
    public async Task<PatientMedicationDetailsResponse?> UpdateAsync(Guid patientUid, Guid medicationUid,
        UpdatePatientMedicationRequest medicationRequest, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/patients/{patientUid}/medications/{medicationUid}")
        { Content = JsonContent.Create(medicationRequest) };
        await AddBearerTokenAsync(request);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PatientMedicationDetailsResponse>(cancellationToken: cancellationToken);
    }

    private async Task AddBearerTokenAsync(
        HttpRequestMessage request)
    {
        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                await GetAccessTokenAsync());
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "No active HTTP context is available.");

        var accessToken = await httpContext.GetTokenAsync(
            "access_token");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new UnauthorizedAccessException(
                "The access token is missing. Sign in again.");
        }

        return accessToken;
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        _logger.LogWarning(
            "MicroEMR API medication request failed. Status: {StatusCode}. Response: {ResponseBody}",
            (int)response.StatusCode,
            responseBody);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException(
                "The API rejected the access token. Sign in again.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to perform this action.");
        }

        throw new HttpRequestException(
            $"MicroEMR API request failed with status " +
            $"{(int)response.StatusCode} ({response.ReasonPhrase}). " +
            $"{responseBody}",
            inner: null,
            statusCode: response.StatusCode);
    }
}
