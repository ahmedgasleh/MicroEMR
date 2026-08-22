using System.Net;
using System.Net.Http.Json;
using MicroEMR.Application.SecurityAudit;

namespace MicroEMR.Web.Services.SecurityAudit;

public interface ISecurityAuditApiClient
{
    Task<SecurityAuditSearchPage> SearchAsync(
        SecurityAuditSearchRequest request, CancellationToken cancellationToken = default);
    Task<SecurityAuditDetail?> GetAsync(
        Guid securityAuditEventUid, CancellationToken cancellationToken = default);
}

public sealed class SecurityAuditApiClient(HttpClient client) : ISecurityAuditApiClient
{
    public async Task<SecurityAuditSearchPage> SearchAsync(
        SecurityAuditSearchRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await client.PostAsJsonAsync(
            "api/platform/security-audit/search", request, cancellationToken);
        await EnsureReviewResponseAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SecurityAuditSearchPage>(cancellationToken: cancellationToken)
            ?? throw new SecurityAuditApiException(
                SecurityAuditApiFailure.Temporary, "The Security Audit response was empty.");
    }

    public async Task<SecurityAuditDetail?> GetAsync(
        Guid securityAuditEventUid, CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(
            $"api/platform/security-audit/events/{securityAuditEventUid:D}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureReviewResponseAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SecurityAuditDetail>(cancellationToken: cancellationToken)
            ?? throw new SecurityAuditApiException(
                SecurityAuditApiFailure.Temporary, "The Security Audit detail response was empty.");
    }

    private static Task EnsureReviewResponseAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (response.IsSuccessStatusCode) return Task.CompletedTask;
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new SecurityAuditApiException(SecurityAuditApiFailure.Unauthorized,
                "Security Audit access is no longer authorized.");
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new SecurityAuditApiException(SecurityAuditApiFailure.Validation,
                "The selected Security Audit filters are invalid.");
        throw new SecurityAuditApiException(SecurityAuditApiFailure.Temporary,
            "Security Audit is temporarily unavailable.");
    }
}

public enum SecurityAuditApiFailure { Validation, Unauthorized, Temporary }

public sealed class SecurityAuditApiException(
    SecurityAuditApiFailure failure, string message) : HttpRequestException(message)
{
    public SecurityAuditApiFailure Failure { get; } = failure;
}
