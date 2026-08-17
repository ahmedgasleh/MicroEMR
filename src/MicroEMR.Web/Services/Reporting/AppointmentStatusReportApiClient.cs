using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Application.Reporting;

namespace MicroEMR.Web.Services.Reporting;

public interface IAppointmentStatusReportApiClient
{
    Task<AppointmentStatusReport> GetAsync(DateOnly startDate, DateOnly endDate, bool auditExecution = true,
        CancellationToken cancellationToken = default);
    Task<byte[]> GetCsvAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
}

public sealed class AppointmentStatusReportApiClient(HttpClient client, IHttpContextAccessor accessor)
    : IAppointmentStatusReportApiClient
{
    public Task<AppointmentStatusReport> GetAsync(DateOnly startDate, DateOnly endDate, bool auditExecution = true,
        CancellationToken cancellationToken = default) =>
        SendAsync<AppointmentStatusReport>($"api/reports/appointments/status?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}&auditExecution={auditExecution.ToString().ToLowerInvariant()}", cancellationToken);

    public Task<byte[]> GetCsvAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        SendBytesAsync($"api/reports/appointments/status/csv?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}", cancellationToken);

    private async Task<T> SendAsync<T>(string uri, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("The appointment status report could not be loaded.", null, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("The appointment status report response was empty.");
    }

    private async Task<byte[]> SendBytesAsync(string uri, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("The appointment status CSV could not be exported.", null, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(string uri, CancellationToken cancellationToken)
    {
        var context = accessor.HttpContext ?? throw new UnauthorizedAccessException();
        var token = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedAccessException();
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, cancellationToken);
    }
}
