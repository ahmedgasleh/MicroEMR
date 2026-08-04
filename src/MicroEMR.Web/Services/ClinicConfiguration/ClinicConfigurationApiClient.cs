using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Web.Models.ClinicConfiguration;

namespace MicroEMR.Web.Services.ClinicConfiguration;

public sealed class ClinicConfigurationApiClient(HttpClient client, IHttpContextAccessor contextAccessor)
    : IClinicConfigurationApiClient
{
    private const string Endpoint = "api/clinic-configuration";

    public async Task<ClinicConfigurationViewModel> GetAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get);
        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ClinicConfigurationViewModel>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("Clinic settings could not be loaded.");
    }

    public async Task<ClinicConfigurationViewModel> SaveAsync(
        SaveClinicConfigurationRequest value,
        CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Put);
        request.Content = JsonContent.Create(value);
        using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ClinicConfigurationViewModel>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("Clinic settings could not be saved.");
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method)
    {
        var context = contextAccessor.HttpContext
            ?? throw new InvalidOperationException("The authenticated request context is unavailable.");
        var token = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException("The API access token is unavailable.");
        var request = new HttpRequestMessage(method, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return Task.CompletedTask;
        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Please correct the clinic settings and try again.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "You are not authorized to manage clinic settings.",
            HttpStatusCode.Conflict => "Clinic settings were changed by another user.",
            _ => "Clinic settings could not be saved."
        };
        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
