using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Application.PatientMedications.Contracts;
using MicroEMR.Application.PatientEncounters;

namespace MicroEMR.Web.Services.PatientEncounters;

public interface IEncounterMedicationApiClient
{
    Task<EncounterMedicationsResponse?> GetAsync(Guid patient, Guid encounter, CancellationToken token = default);
    Task<PatientMedicationDetailsResponse?> SaveAsync(Guid patient, Guid encounter, SaveEncounterMedicationRequest input, CancellationToken token = default);
}

public sealed class EncounterMedicationApiClient(HttpClient http, IHttpContextAccessor contexts) : IEncounterMedicationApiClient
{
    public Task<EncounterMedicationsResponse?> GetAsync(Guid patient, Guid encounter, CancellationToken token = default) =>
        Send<EncounterMedicationsResponse>(patient, encounter, null, token);
    public Task<PatientMedicationDetailsResponse?> SaveAsync(Guid patient, Guid encounter, SaveEncounterMedicationRequest input, CancellationToken token = default) =>
        Send<PatientMedicationDetailsResponse>(patient, encounter, input, token);

    private async Task<T?> Send<T>(Guid patient, Guid encounter, SaveEncounterMedicationRequest? input, CancellationToken token)
    {
        var context = contexts.HttpContext ?? throw new InvalidOperationException("An HTTP context is required.");
        var bearer = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(bearer)) throw new UnauthorizedAccessException("The access token is missing.");
        using var request = new HttpRequestMessage(input is null ? HttpMethod.Get : HttpMethod.Post,
            $"api/patients/{patient}/encounters/{encounter}/medications");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (input is not null) request.Content = JsonContent.Create(input);
        using var response = await http.SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return default;
        if (!response.IsSuccessStatusCode)
        {
            var detail = "Medication could not be saved or loaded.";
            try
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                if (json.RootElement.TryGetProperty("detail", out var value) && value.ValueKind == JsonValueKind.String) detail = value.GetString() ?? detail;
            }
            catch (JsonException) { }
            throw new HttpRequestException(detail, null, response.StatusCode);
        }
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: token);
    }
}
