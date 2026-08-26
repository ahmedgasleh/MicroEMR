using System.Net.Http.Json;
using MicroEMR.Application.Cds;

namespace MicroEMR.Web.Services.Cds;

public interface ICdsApiClient
{
    Task<CdsEvaluationResponse> EvaluateAsync(Guid patientUid, CancellationToken cancellationToken);
    Task<IReadOnlyList<CdsAlertHistoryResponse>> HistoryAsync(Guid patientUid, Guid alertUid, CancellationToken cancellationToken);
    Task<CdsAlertResponse?> AcknowledgeAsync(Guid patientUid, Guid alertUid, AcknowledgeCdsAlertRequest request, CancellationToken cancellationToken);
    Task<CdsAlertResponse?> DismissAsync(Guid patientUid, Guid alertUid, DismissCdsAlertRequest request, CancellationToken cancellationToken);
}

public sealed class CdsApiClient(HttpClient client) : ICdsApiClient
{
    public async Task<CdsEvaluationResponse> EvaluateAsync(Guid patientUid, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsync($"api/patients/{patientUid}/cds/evaluate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CdsEvaluationResponse>(cancellationToken: cancellationToken)
            ?? new([], 0, 0);
    }

    public async Task<IReadOnlyList<CdsAlertHistoryResponse>> HistoryAsync(Guid patientUid, Guid alertUid,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"api/patients/{patientUid}/cds/{alertUid}/history", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<CdsAlertHistoryResponse>>(cancellationToken: cancellationToken) ?? [];
    }

    public Task<CdsAlertResponse?> AcknowledgeAsync(Guid patientUid, Guid alertUid,
        AcknowledgeCdsAlertRequest request, CancellationToken cancellationToken) =>
        RespondAsync($"api/patients/{patientUid}/cds/{alertUid}/acknowledge", request, cancellationToken);

    public Task<CdsAlertResponse?> DismissAsync(Guid patientUid, Guid alertUid,
        DismissCdsAlertRequest request, CancellationToken cancellationToken) =>
        RespondAsync($"api/patients/{patientUid}/cds/{alertUid}/dismiss", request, cancellationToken);

    private async Task<CdsAlertResponse?> RespondAsync(string uri, object request, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(uri, request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            throw new HttpRequestException("The CDS finding changed. Reload and try again.", null, response.StatusCode);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CdsAlertResponse>(cancellationToken: cancellationToken);
    }
}
