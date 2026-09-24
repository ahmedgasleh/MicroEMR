using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MicroEMR.Application.PatientCareTeam;

namespace MicroEMR.Web.Services.PatientCareTeam;

public interface IPatientCareTeamApiClient
{
    Task<IReadOnlyList<PatientCareTeamRelationship>> List(Guid patientUid, CancellationToken token);
    Task<IReadOnlyList<CareTeamRelationshipType>> Types(Guid patientUid, CancellationToken token);
    Task<PatientCareTeamRelationship> Add(Guid patientUid, AddPatientCareTeamRelationshipRequest request, CancellationToken token);
    Task<PatientCareTeamRelationship> Update(Guid patientUid, Guid relationshipUid, UpdatePatientCareTeamRelationshipRequest request, CancellationToken token);
    Task<PatientCareTeamRelationship> End(Guid patientUid, Guid relationshipUid, EndPatientCareTeamRelationshipRequest request, CancellationToken token);
}

public sealed class PatientCareTeamApiClient(HttpClient client) : IPatientCareTeamApiClient
{
    private static string Root(Guid patientUid) => $"api/patients/{patientUid}/care-team";

    public async Task<IReadOnlyList<PatientCareTeamRelationship>> List(Guid patientUid, CancellationToken token) =>
        await Get<PatientCareTeamRelationship>(Root(patientUid), token);

    public async Task<IReadOnlyList<CareTeamRelationshipType>> Types(Guid patientUid, CancellationToken token) =>
        await Get<CareTeamRelationshipType>($"{Root(patientUid)}/types", token);

    public Task<PatientCareTeamRelationship> Add(Guid patientUid, AddPatientCareTeamRelationshipRequest request, CancellationToken token) =>
        Send(HttpMethod.Post, Root(patientUid), request, token);

    public Task<PatientCareTeamRelationship> Update(Guid patientUid, Guid relationshipUid, UpdatePatientCareTeamRelationshipRequest request, CancellationToken token) =>
        Send(HttpMethod.Put, $"{Root(patientUid)}/{relationshipUid}", request, token);

    public Task<PatientCareTeamRelationship> End(Guid patientUid, Guid relationshipUid, EndPatientCareTeamRelationshipRequest request, CancellationToken token) =>
        Send(HttpMethod.Post, $"{Root(patientUid)}/{relationshipUid}/end", request, token);

    private async Task<IReadOnlyList<T>> Get<T>(string url, CancellationToken token)
    {
        using var response = await client.GetAsync(url, token);
        await Ensure(response, token);
        return await response.Content.ReadFromJsonAsync<T[]>(cancellationToken: token) ?? [];
    }

    private async Task<PatientCareTeamRelationship> Send(HttpMethod method, string url, object request, CancellationToken token)
    {
        using var message = new HttpRequestMessage(method, url) { Content = JsonContent.Create(request) };
        using var response = await client.SendAsync(message, token);
        await Ensure(response, token);
        return (await response.Content.ReadFromJsonAsync<PatientCareTeamRelationship>(cancellationToken: token))!;
    }

    private static async Task Ensure(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        ApiError? error;
        try { error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: token); }
        catch (JsonException) { error = null; }
        var message = response.StatusCode switch
        {
            HttpStatusCode.Forbidden => "You do not have permission to manage this care team.",
            HttpStatusCode.NotFound => "Patient or relationship was not found.",
            _ => error?.Message ?? "Care team request failed."
        };
        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private sealed record ApiError(string? Message);
}
