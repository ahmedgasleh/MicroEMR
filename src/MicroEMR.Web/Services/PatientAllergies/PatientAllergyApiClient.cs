using System.Net;
using System.Net.Http.Headers;
using MicroEMR.Application.OperationalTelemetry;
using MicroEMR.Web.Services;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Web.Models.PatientAllergies;

namespace MicroEMR.Web.Services.PatientAllergies;

public sealed class PatientAllergyApiClient : IPatientAllergyApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PatientAllergyApiClient> _logger;

    public PatientAllergyApiClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PatientAllergyApiClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PatientAllergyListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/patients/{patientUid}/allergies");

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var allergies =
            await response.Content.ReadFromJsonAsync<
                List<PatientAllergyListItemResponse>>(
                cancellationToken: cancellationToken);

        return allergies ?? [];
    }

    public async Task<PatientAllergyDetailsResponse?> GetByUidAsync(
        Guid allergyUid,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/patient-allergies/{allergyUid}");

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
            .ReadFromJsonAsync<PatientAllergyDetailsResponse>(
                cancellationToken: cancellationToken);
    }

    public async Task<AllergyDocumentationStateResponse?> GetDocumentationStateAsync(Guid patientUid, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/patients/{patientUid}/allergies/documentation-state");
        await AddBearerTokenAsync(request);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AllergyDocumentationStateResponse>(cancellationToken: cancellationToken);
    }

    public async Task<NoKnownAllergiesAssertionResponse> AssertNoKnownAllergiesAsync(Guid patientUid, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/patients/{patientUid}/allergies/no-known-allergies") { Content = JsonContent.Create(new { }) };
        await AddBearerTokenAsync(request);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<NoKnownAllergiesAssertionResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The API returned no NKA assertion.");
    }

    public async Task<NoKnownAllergiesAssertionResponse?> RevokeNoKnownAllergiesAsync(Guid patientUid, RevokeNoKnownAllergiesRequest revokeRequest, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/patients/{patientUid}/allergies/no-known-allergies/revoke") { Content = JsonContent.Create(revokeRequest) };
        await AddBearerTokenAsync(request);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<NoKnownAllergiesAssertionResponse>(cancellationToken: cancellationToken);
    }

    public async Task<PatientAllergyDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientAllergyRequest allergyRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/patients/{patientUid}/allergies")
        {
            Content = JsonContent.Create(allergyRequest)
        };

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content
                   .ReadFromJsonAsync<PatientAllergyDetailsResponse>(
                       cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException(
                   "The API created the allergy but returned no allergy data.");
    }

    public async Task<PatientAllergyDetailsResponse?> UpdateAsync(
        Guid patientUid,
        Guid allergyUid,
        UpdatePatientAllergyRequest allergyRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"api/patients/{patientUid}/allergies/{allergyUid}")
        {
            Content = JsonContent.Create(allergyRequest)
        };
        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PatientAllergyDetailsResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "The API updated the allergy but returned no allergy data.");
    }

    public async Task<PatientAllergyDetailsResponse?> ResolveAsync(Guid patientUid, Guid allergyUid,
        ResolvePatientAllergyRequest allergyRequest, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/patients/{patientUid}/allergies/{allergyUid}/resolve")
        { Content = JsonContent.Create(allergyRequest) };
        await AddBearerTokenAsync(request);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PatientAllergyDetailsResponse>(cancellationToken: cancellationToken);
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

        _logger.HttpDependencyFailed("PatientAllergyApi", (int)response.StatusCode);

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

        throw new SafeApiResponseException(response.StatusCode, responseBody);
    }
}
