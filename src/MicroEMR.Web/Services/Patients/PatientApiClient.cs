using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using MicroEMR.Web.Models.Patients;

namespace MicroEMR.Web.Services.Patients;

public sealed class PatientApiClient : IPatientApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PatientApiClient> _logger;

    public PatientApiClient (
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PatientApiClient> logger )
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<PatientSearchResponse> SearchAsync (
        string? searchText,
        DateOnly? dateOfBirth,
        int pageNumber = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default )
    {
        var queryParameters = new Dictionary<string, string?>
        {
            ["searchText"] = string.IsNullOrWhiteSpace(searchText)
                ? null
                : searchText.Trim(),

            ["dateOfBirth"] = dateOfBirth?.ToString("yyyy-MM-dd"),

            ["pageNumber"] = Math.Max(pageNumber, 1).ToString(),

            ["pageSize"] = Math.Clamp(pageSize, 1, 100).ToString()
        };

        var requestUri = QueryHelpers.AddQueryString(
            "/api/patients",
            queryParameters);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            requestUri);

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content
                   .ReadFromJsonAsync<PatientSearchResponse>(
                       cancellationToken: cancellationToken)
               ?? new PatientSearchResponse();
    }

    public async Task<PatientDetailsResponse?> GetByUidAsync (
        Guid patientUid,
        CancellationToken cancellationToken = default )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/patients/{patientUid}");

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
            .ReadFromJsonAsync<PatientDetailsResponse>(
                cancellationToken: cancellationToken);
    }

    public async Task<PatientDetailsResponse> CreateAsync (
        CreatePatientRequest patientRequest,
        CancellationToken cancellationToken = default )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/patients")
        {
            Content = JsonContent.Create(patientRequest)
        };

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content
                   .ReadFromJsonAsync<PatientDetailsResponse>(
                       cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException(
                   "The API created the patient but returned no patient data.");
    }

    private async Task AddBearerTokenAsync (
        HttpRequestMessage request )
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

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);
    }

    private async Task EnsureSuccessAsync (
        HttpResponseMessage response,
        CancellationToken cancellationToken )
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(
            cancellationToken);

        _logger.LogWarning(
            "MicroEMR API request failed. Status: {StatusCode}. Response: {ResponseBody}",
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