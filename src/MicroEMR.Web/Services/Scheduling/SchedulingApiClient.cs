using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using MicroEMR.Web.Models.Scheduling;
using System.Text.Json;

namespace MicroEMR.Web.Services.Scheduling;

public sealed class SchedulingApiClient : ISchedulingApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SchedulingApiClient> _logger;

    public SchedulingApiClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SchedulingApiClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ScheduleResourceResponse>>
        GetActiveResourcesAsync(
            CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "api/scheduling/resources");

        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var resources =
            await response.Content.ReadFromJsonAsync<
                List<ScheduleResourceResponse>>(
                cancellationToken: cancellationToken);

        return resources ?? [];
    }

    public async Task<IReadOnlyList<ScheduleAppointmentListItemResponse>>
        GetAppointmentsAsync(
            DateTime startUtc,
            DateTime endUtc,
            Guid? resourceUid,
            CancellationToken cancellationToken = default)
    {
        var queryParameters = new Dictionary<string, string?>
        {
            ["startUtc"] = NormalizeUtc(startUtc).ToString("O"),
            ["endUtc"] = NormalizeUtc(endUtc).ToString("O"),
            ["resourceUid"] = resourceUid?.ToString()
        };

        var requestUri = QueryHelpers.AddQueryString(
            "api/scheduling/appointments",
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

        var appointments =
            await response.Content.ReadFromJsonAsync<
                List<ScheduleAppointmentListItemResponse>>(
                cancellationToken: cancellationToken);

        return appointments ?? [];
    }

    public async Task<ScheduleAppointmentListItemResponse> CreateAppointmentAsync(
        CreateScheduleAppointmentRequest appointmentRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/scheduling/appointments")
        {
            Content = JsonContent.Create(appointmentRequest)
        };
        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ScheduleAppointmentListItemResponse>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException(
                   "The API created the appointment but returned no appointment data.");
    }

    public async Task<IReadOnlyList<ScheduleMonthSummaryItemResponse>> GetMonthSummaryAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
    {
        var requestUri = QueryHelpers.AddQueryString(
            "api/scheduling/month-summary",
            new Dictionary<string, string?>
            {
                ["startUtc"] = NormalizeUtc(startUtc).ToString("O"),
                ["endUtc"] = NormalizeUtc(endUtc).ToString("O")
            });
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        await AddBearerTokenAsync(request);
        using var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<ScheduleMonthSummaryItemResponse>>(
            cancellationToken: cancellationToken) ?? [];
    }

    public async Task<ScheduleAppointmentDetailsResponse?> GetAppointmentByUidAsync(
        Guid appointmentUid,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/scheduling/appointments/{appointmentUid}");
        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ScheduleAppointmentDetailsResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<AppointmentHistoryResponse>> GetAppointmentHistoryAsync(
        Guid appointmentUid,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/scheduling/appointments/{appointmentUid}/history");
        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<AppointmentHistoryResponse>>(
            cancellationToken: cancellationToken) ?? [];
    }

    public async Task<CancelScheduleAppointmentResponse?> CancelAppointmentAsync(
        Guid appointmentUid,
        CancelScheduleAppointmentRequest appointmentRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/scheduling/appointments/{appointmentUid}/cancel")
        {
            Content = JsonContent.Create(appointmentRequest)
        };
        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CancelScheduleAppointmentResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "The API cancelled the appointment but returned no cancellation data.");
    }

    public async Task<ScheduleAppointmentDetailsResponse?> UpdateAppointmentAsync(
        Guid appointmentUid,
        UpdateScheduleAppointmentRequest appointmentRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"api/scheduling/appointments/{appointmentUid}")
        {
            Content = JsonContent.Create(appointmentRequest)
        };
        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "MicroEMR API scheduling update returned a conflict. Status: {StatusCode}.",
                (int)response.StatusCode);
            var isCancelled = false;
            try
            {
                using var document = JsonDocument.Parse(body);
                isCancelled = document.RootElement.TryGetProperty("code", out var code)
                    && code.GetString() == "appointment_cancelled";
            }
            catch (JsonException)
            {
                // Treat malformed conflict responses as scheduling overlaps.
            }
            throw new AppointmentUpdateConflictException(isCancelled);
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ScheduleAppointmentDetailsResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "The API updated the appointment but returned no appointment data.");
    }

    public async Task<ScheduleAppointmentDetailsResponse?> RescheduleAppointmentAsync(
        Guid appointmentUid,
        RescheduleAppointmentRequest appointmentRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/scheduling/appointments/{appointmentUid}/reschedule")
        {
            Content = JsonContent.Create(appointmentRequest)
        };
        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "MicroEMR API scheduling reschedule returned a conflict. Status: {StatusCode}.",
                (int)response.StatusCode);
            var isCancelled = false;
            try
            {
                using var document = JsonDocument.Parse(body);
                isCancelled = document.RootElement.TryGetProperty("code", out var code)
                    && code.GetString() == "appointment_cancelled";
            }
            catch (JsonException)
            {
                // Treat malformed conflict responses as scheduling overlaps.
            }
            throw new AppointmentUpdateConflictException(isCancelled);
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ScheduleAppointmentDetailsResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "The API rescheduled the appointment but returned no appointment data.");
    }

    public async Task<UpdateAppointmentStatusResponse?> UpdateAppointmentStatusAsync(
        Guid appointmentUid,
        UpdateAppointmentStatusRequest appointmentRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/scheduling/appointments/{appointmentUid}/status")
        {
            Content = JsonContent.Create(appointmentRequest)
        };
        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "MicroEMR API appointment status update conflicted. Status: {StatusCode}. Response: {ResponseBody}",
                (int)response.StatusCode,
                responseBody);
            throw new AppointmentStatusConflictException();
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<UpdateAppointmentStatusResponse>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException(
                   "The API updated the appointment status but returned no status data.");
    }

    public async Task<StartEncounterFromAppointmentResponse?> StartEncounterFromAppointmentAsync(
        Guid appointmentUid,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/scheduling/appointments/{appointmentUid}/start-encounter");
        await AddBearerTokenAsync(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var isCompleted = false;
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                isCompleted = document.RootElement.TryGetProperty("code", out var code)
                    && code.GetString() == "appointment_completed";
            }
            catch (JsonException)
            {
                // Preserve a friendly conflict response if the API body is malformed.
            }

            _logger.LogWarning(
                "MicroEMR API rejected encounter start with status {StatusCode}.",
                (int)response.StatusCode);
            throw new StartEncounterConflictException(isCompleted);
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<StartEncounterFromAppointmentResponse>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException(
                   "The API started the encounter but returned no encounter data.");
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Local)
                .ToUniversalTime();
        }

        return value.ToUniversalTime();
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

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogWarning(
            "MicroEMR API scheduling request failed. Status: {StatusCode}. Response: {ResponseBody}",
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
            $"MicroEMR API scheduling request failed with status " +
            $"{(int)response.StatusCode} ({response.ReasonPhrase}).",
            inner: null,
            statusCode: response.StatusCode);
    }
}
