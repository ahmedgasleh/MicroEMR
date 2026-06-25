using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Web.Models.PatientDocuments;

namespace MicroEMR.Web.Services.PatientDocuments;

public sealed class PatientDocumentApiClient
    : IPatientDocumentApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PatientDocumentApiClient> _logger;

    public PatientDocumentApiClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PatientDocumentApiClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<
        IReadOnlyList<PatientDocumentListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/patients/{patientUid}/documents");

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var documents =
            await response.Content.ReadFromJsonAsync<
                List<PatientDocumentListItemResponse>>(
                cancellationToken: cancellationToken);

        return documents ?? [];
    }

    public async Task<PatientDocumentDetailsResponse?> GetByUidAsync(
        Guid documentUid,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/patient-documents/{documentUid}");

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
            .ReadFromJsonAsync<PatientDocumentDetailsResponse>(
                cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentTemplateListItemResponse>>
        GetActiveTemplatesAsync(
            CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "api/document-templates");

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var templates =
            await response.Content.ReadFromJsonAsync<
                List<DocumentTemplateListItemResponse>>(
                cancellationToken: cancellationToken);

        return templates ?? [];
    }

    public async Task<DocumentTemplateDetailsResponse?> GetTemplateByUidAsync(
        Guid templateUid,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/document-templates/{templateUid}");

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
            .ReadFromJsonAsync<DocumentTemplateDetailsResponse>(
                cancellationToken: cancellationToken);
    }

    public async Task<PatientDocumentDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientDocumentRequest documentRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/patients/{patientUid}/documents")
        {
            Content = JsonContent.Create(documentRequest)
        };

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content
                   .ReadFromJsonAsync<PatientDocumentDetailsResponse>(
                       cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException(
                   "The API created the document but returned no document data.");
    }

    private async Task AddBearerTokenAsync(
        HttpRequestMessage request)
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
            "MicroEMR API document request failed. Status: {StatusCode}. Response: {ResponseBody}",
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
