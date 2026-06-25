using System.Net;
using System.Net.Http.Json;
using MicroEMR.Web.Models.PatientDocuments;

namespace MicroEMR.Web.Services.PatientDocuments;

public sealed class PatientDocumentApiClient
    : IPatientDocumentApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PatientDocumentApiClient> _logger;

    public PatientDocumentApiClient(
        HttpClient httpClient,
        ILogger<PatientDocumentApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<
        IReadOnlyList<PatientDocumentListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        using var response =
            await _httpClient.GetAsync(
                $"api/patients/{patientUid}/documents",
                cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Array.Empty<PatientDocumentListItemResponse>();
        }

        if (!response.IsSuccessStatusCode)
        {
            var responseBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            _logger.LogError(
                "Failed to load documents for patient {PatientUid}. " +
                "Status code: {StatusCode}. Response: {ResponseBody}",
                patientUid,
                response.StatusCode,
                responseBody);

            response.EnsureSuccessStatusCode();
        }

        var documents =
            await response.Content.ReadFromJsonAsync<
                List<PatientDocumentListItemResponse>>(
                cancellationToken: cancellationToken);

        return documents ?? new List<PatientDocumentListItemResponse>();
    }

    public async Task<PatientDocumentDetailsResponse?> GetByUidAsync(
        Guid documentUid,
        CancellationToken cancellationToken = default)
    {
        using var response =
            await _httpClient.GetAsync(
                $"api/patient-documents/{documentUid}",
                cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var responseBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            _logger.LogError(
                "Failed to load patient document {DocumentUid}. " +
                "Status code: {StatusCode}. Response: {ResponseBody}",
                documentUid,
                response.StatusCode,
                responseBody);

            response.EnsureSuccessStatusCode();
        }

        return await response.Content
            .ReadFromJsonAsync<PatientDocumentDetailsResponse>(
                cancellationToken: cancellationToken);
    }
}