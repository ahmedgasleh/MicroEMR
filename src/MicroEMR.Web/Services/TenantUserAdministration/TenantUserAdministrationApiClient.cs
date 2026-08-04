using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using MicroEMR.Web.Models.TenantUserAdministration;

namespace MicroEMR.Web.Services.TenantUserAdministration;

public interface ITenantUserAdministrationApiClient
{
    Task<IReadOnlyList<TenantUserAdministrationItemViewModel>> GetUsersAsync(
        CancellationToken cancellationToken = default);
    Task<TenantUserAdministrationItemViewModel> DeactivateAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default);
    Task<TenantUserAdministrationItemViewModel> ActivateAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default);
}

public sealed class TenantUserAdministrationApiClient(
    HttpClient client,
    IHttpContextAccessor contextAccessor) : ITenantUserAdministrationApiClient
{
    public async Task<IReadOnlyList<TenantUserAdministrationItemViewModel>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var context = contextAccessor.HttpContext
            ?? throw new InvalidOperationException("The authenticated request context is unavailable.");
        var token = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException("The API access token is unavailable.");

        using var request = new HttpRequestMessage(HttpMethod.Get, "api/admin/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    "You are not authorized to view user administration.",
                _ => "User administration could not be loaded."
            };
            throw new HttpRequestException(message, null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<List<TenantUserAdministrationItemViewModel>>(
            cancellationToken: cancellationToken) ?? [];
    }

    public Task<TenantUserAdministrationItemViewModel> DeactivateAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(authUserId, "deactivate", rowVersion, cancellationToken);

    public Task<TenantUserAdministrationItemViewModel> ActivateAsync(string authUserId, string rowVersion,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(authUserId, "activate", rowVersion, cancellationToken);

    private async Task<TenantUserAdministrationItemViewModel> ChangeAsync(string authUserId, string action,
        string rowVersion, CancellationToken cancellationToken)
    {
        var context = contextAccessor.HttpContext
            ?? throw new InvalidOperationException("The authenticated request context is unavailable.");
        var token = await context.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedAccessException("The API access token is unavailable.");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"api/admin/users/{Uri.EscapeDataString(authUserId)}/membership/{action}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { rowVersion });
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode switch
            {
                HttpStatusCode.NotFound => "The membership was not found in this clinic.",
                HttpStatusCode.Conflict => "This membership was changed or cannot be changed. The latest status has been reloaded.",
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "You are not authorized to change this membership.",
                _ => "The membership could not be changed."
            };
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        return await response.Content.ReadFromJsonAsync<TenantUserAdministrationItemViewModel>(cancellationToken: cancellationToken)
            ?? throw new HttpRequestException("The membership could not be changed.");
    }
}
