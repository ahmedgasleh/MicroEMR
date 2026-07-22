using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Web.Models.PatientEncounters;

namespace MicroEMR.Web.Services.PatientEncounters;

public sealed class PatientEncounterApiClient
    : IPatientEncounterApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PatientEncounterApiClient> _logger;

    public PatientEncounterApiClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PatientEncounterApiClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PatientEncounterListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/patients/{patientUid}/encounters");

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var encounters =
            await response.Content.ReadFromJsonAsync<
                List<PatientEncounterListItemResponse>>(
                cancellationToken: cancellationToken);

        return encounters ?? [];
    }

    public async Task<PatientEncounterDetailsResponse?> GetByUidAsync(
        Guid encounterUid,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/patient-encounters/{encounterUid}");

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
            .ReadFromJsonAsync<PatientEncounterDetailsResponse>(
                cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PatientEncounterHistoryResponse>> GetEncounterHistoryAsync(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/patients/{patientUid}/encounters/{encounterUid}/history");
        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<
            List<PatientEncounterHistoryResponse>>(
                cancellationToken: cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<PatientEncounterAddendumResponse>> GetEncounterAddendumsAsync(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"api/patients/{patientUid}/encounters/{encounterUid}/addendums");
        await AddBearerTokenAsync(request);
        using var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return [];
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<PatientEncounterAddendumResponse>>(
            cancellationToken: cancellationToken) ?? [];
    }

    public async Task<PatientEncounterAddendumResponse?> CreateEncounterAddendumAsync(
        Guid patientUid,
        Guid encounterUid,
        CreateEncounterAddendumRequest addendumRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"api/patients/{patientUid}/encounters/{encounterUid}/addendums")
        {
            Content = JsonContent.Create(addendumRequest)
        };
        await AddBearerTokenAsync(request);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PatientEncounterAddendumResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<PatientEncounterDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientEncounterRequest encounterRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/patients/{patientUid}/encounters")
        {
            Content = JsonContent.Create(encounterRequest)
        };

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content
                   .ReadFromJsonAsync<PatientEncounterDetailsResponse>(
                       cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException(
                   "The API created the encounter but returned no encounter data.");
    }

    public async Task<PatientEncounterDetailsResponse?> UpdateNoteAsync(
        Guid patientUid,
        Guid encounterUid,
        UpdateEncounterNoteRequest encounterRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"api/patients/{patientUid}/encounters/{encounterUid}/note")
        {
            Content = JsonContent.Create(encounterRequest)
        };

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content
            .ReadFromJsonAsync<PatientEncounterDetailsResponse>(
                cancellationToken: cancellationToken);
    }

    public async Task<PatientEncounterDetailsResponse?> UpdateEncounterSoapNoteAsync(
        Guid patientUid,
        Guid encounterUid,
        UpdateEncounterSoapNoteRequest soapNoteRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"api/patients/{patientUid}/encounters/{encounterUid}/soap-note")
        {
            Content = JsonContent.Create(soapNoteRequest)
        };
        await AddBearerTokenAsync(request);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PatientEncounterDetailsResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<PatientEncounterDetailsResponse?> SignEncounterAsync(
        Guid patientUid,
        Guid encounterUid,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/patients/{patientUid}/encounters/{encounterUid}/sign");

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content
            .ReadFromJsonAsync<PatientEncounterDetailsResponse>(
                cancellationToken: cancellationToken);
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
            "MicroEMR API encounter request failed. Status: {StatusCode}. Response: {ResponseBody}",
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
