using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Application.ClinicConfiguration;

namespace MicroEMR.Web.Services.ClinicConfiguration;

public interface IClinicBrandingApiClient
{
    Task<ClinicLogoState> GetStateAsync(CancellationToken token = default);
    Task SaveAsync(IFormFile? logo, bool remove, string? rowVersion, CancellationToken token = default);
    Task<byte[]?> GetLogoAsync(CancellationToken token = default);
    Task<string> GetHeaderAsync(CancellationToken token = default);
}

public sealed class ClinicBrandingApiClient(HttpClient client, IHttpContextAccessor context) : IClinicBrandingApiClient
{
    public async Task<ClinicLogoState> GetStateAsync(CancellationToken token = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "", null, token);
        return await response.Content.ReadFromJsonAsync<ClinicLogoState>(cancellationToken:token) ?? throw new HttpRequestException("Clinic branding unavailable.");
    }

    public async Task SaveAsync(IFormFile? logo, bool remove, string? rowVersion, CancellationToken token = default)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new StringContent(remove.ToString()), "remove");
        if (rowVersion is not null) body.Add(new StringContent(rowVersion), "rowVersion");
        if (logo is not null)
        {
            body.Add(new StreamContent(logo.OpenReadStream()), "logo", "logo");
        }
        using var response = await SendAsync(HttpMethod.Post, "/logo", body, token);
    }

    public async Task<byte[]?> GetLogoAsync(CancellationToken token = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "/logo", null, token, allowMissing:true);
        return response.StatusCode == System.Net.HttpStatusCode.NotFound ? null : await response.Content.ReadAsByteArrayAsync(token);
    }

    public async Task<string> GetHeaderAsync(CancellationToken token = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "/header", null, token);
        return await response.Content.ReadAsStringAsync(token);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string suffix, HttpContent? content, CancellationToken token, bool allowMissing = false)
    {
        var http = context.HttpContext ?? throw new UnauthorizedAccessException();
        var accessToken = await http.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(accessToken)) throw new UnauthorizedAccessException();
        using var request = new HttpRequestMessage(method, "api/clinic-branding" + suffix) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request, token);
        if (response.IsSuccessStatusCode || (allowMissing && response.StatusCode == System.Net.HttpStatusCode.NotFound)) return response;
        var status = response.StatusCode;
        response.Dispose();
        throw new HttpRequestException("Clinic branding request failed.", null, status);
    }
}
