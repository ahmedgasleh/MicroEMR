namespace MicroEMR.Application.SecurityAudit;

public static class SecurityAuditDenialReasons
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "MissingPermission", "CrossPatientOwnership", "UnresolvedClinicalActor", "InvalidTenantMembership"
    };
}

public sealed record SecurityAuditSearchRequest(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int? PageSize = null,
    string? ContinuationToken = null,
    string? DenialReason = null,
    string? Capability = null,
    string? SourceApplication = null,
    Guid? TargetTenantUid = null,
    string? RequestCorrelationId = null,
    string? ActorSubject = null);

public sealed record SecurityAuditSearchCriteria(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int PageSize,
    DateTimeOffset? CursorOccurredAtUtc,
    Guid? CursorSecurityAuditEventUid,
    string? DenialReason,
    string? Capability,
    string? SourceApplication,
    Guid? TargetTenantUid,
    string? RequestCorrelationId,
    string? ActorSubject);

public sealed record SecurityAuditListItem(
    Guid SecurityAuditEventUid,
    DateTimeOffset OccurredAtUtc,
    string DenialReason,
    string Capability,
    string? RequiredPermission,
    string SourceApplication,
    Guid? TargetTenantUid,
    string? RequestCorrelationId,
    string MaskedActorSubject);

public sealed record SecurityAuditDetail(
    Guid SecurityAuditEventUid,
    string EventType,
    string Outcome,
    string DenialReason,
    string ActorSubject,
    long? ClinicalUserId,
    Guid? TargetTenantUid,
    Guid? RequestedTenantUid,
    string Capability,
    string? RequiredPermission,
    string SourceApplication,
    string? RequestCorrelationId,
    Guid? RequestedPatientUid,
    Guid? AuthoritativePatientUid,
    string? ResourceType,
    Guid? ResourceUid,
    DateTimeOffset OccurredAtUtc);

public sealed record SecurityAuditSearchPage(
    IReadOnlyList<SecurityAuditListItem> Items,
    string? ContinuationToken,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int PageSize);

public sealed record SecurityAuditContinuation(
    DateTimeOffset OccurredAtUtc, Guid SecurityAuditEventUid, string FilterFingerprint);

public interface ISecurityAuditContinuationTokenProtector
{
    string Protect(SecurityAuditContinuation continuation);
    bool TryUnprotect(string token, out SecurityAuditContinuation continuation);
}

public interface IPlatformSecurityAuditReviewRepository
{
    Task<IReadOnlyList<SecurityAuditListItem>> SearchAsync(
        SecurityAuditSearchCriteria criteria, CancellationToken cancellationToken = default);
    Task<SecurityAuditDetail?> GetByUidAsync(
        Guid securityAuditEventUid, CancellationToken cancellationToken = default);
    Task RecordReviewAsync(
        string actorSubject, string action, Guid correlationId, Guid? securityAuditEventUid,
        int? resultCount, string? filterSummary, CancellationToken cancellationToken = default);
}

public interface IPlatformSecurityAuditReviewService
{
    Task<SecurityAuditSearchPage> SearchAsync(
        SecurityAuditSearchRequest request, string actorSubject, Guid correlationId,
        CancellationToken cancellationToken = default);
    Task<SecurityAuditDetail?> GetByUidAsync(
        Guid securityAuditEventUid, string actorSubject, Guid correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class SecurityAuditReviewValidationException(string message) : ArgumentException(message);
public sealed class SecurityAuditDisclosureUnavailableException(string message, Exception inner)
    : Exception(message, inner);

public sealed class PlatformSecurityAuditReviewService(
    IPlatformSecurityAuditReviewRepository repository,
    ISecurityAuditContinuationTokenProtector tokenProtector,
    TimeProvider timeProvider) : IPlatformSecurityAuditReviewService
{
    public const int DefaultPageSize = 25;
    public const int MaximumPageSize = 100;
    private static readonly TimeSpan DefaultRange = TimeSpan.FromHours(24);
    private static readonly TimeSpan MaximumRange = TimeSpan.FromDays(31);

    public async Task<SecurityAuditSearchPage> SearchAsync(
        SecurityAuditSearchRequest request, string actorSubject, Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalized = Normalize(request);
        var fingerprint = Fingerprint(normalized);
        var criteria = WithContinuation(normalized, request.ContinuationToken, fingerprint);
        IReadOnlyList<SecurityAuditListItem> rows;
        try
        {
            rows = await repository.SearchAsync(criteria, cancellationToken);
            var disclosed = rows.Take(criteria.PageSize).ToArray();
            await repository.RecordReviewAsync(
                Required(actorSubject, 450, nameof(actorSubject)), "SecurityAuditSearched", correlationId,
                null, disclosed.Length, FilterSummary(criteria), cancellationToken);
            var continuation = rows.Count > criteria.PageSize
                ? tokenProtector.Protect(new SecurityAuditContinuation(
                    disclosed[^1].OccurredAtUtc, disclosed[^1].SecurityAuditEventUid, fingerprint))
                : null;
            return new SecurityAuditSearchPage(
                disclosed, continuation,
                criteria.FromUtc, criteria.ToUtc, criteria.PageSize);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (SecurityAuditReviewValidationException) { throw; }
        catch (Exception exception)
        {
            throw new SecurityAuditDisclosureUnavailableException(
                "Security audit review is temporarily unavailable.", exception);
        }
    }

    public async Task<SecurityAuditDetail?> GetByUidAsync(
        Guid securityAuditEventUid, string actorSubject, Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        if (securityAuditEventUid == Guid.Empty)
            throw new SecurityAuditReviewValidationException("A valid security audit event identifier is required.");
        try
        {
            var detail = await repository.GetByUidAsync(securityAuditEventUid, cancellationToken);
            if (detail is null) return null;
            await repository.RecordReviewAsync(
                Required(actorSubject, 450, nameof(actorSubject)), "SecurityAuditViewed", correlationId,
                securityAuditEventUid, null, null, cancellationToken);
            return detail;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (SecurityAuditReviewValidationException) { throw; }
        catch (Exception exception)
        {
            throw new SecurityAuditDisclosureUnavailableException(
                "Security audit review is temporarily unavailable.", exception);
        }
    }

    private SecurityAuditSearchCriteria Normalize(SecurityAuditSearchRequest request)
    {
        var now = timeProvider.GetUtcNow();
        var to = (request.ToUtc ?? now).ToUniversalTime();
        var from = (request.FromUtc ?? to.Subtract(DefaultRange)).ToUniversalTime();
        if (from >= to || to - from > MaximumRange || from > now || to > now.AddMinutes(1))
            throw new SecurityAuditReviewValidationException("The UTC date range is invalid or exceeds 31 days.");
        var pageSize = request.PageSize ?? DefaultPageSize;
        if (pageSize is < 1 or > MaximumPageSize)
            throw new SecurityAuditReviewValidationException("Page size must be between 1 and 100.");
        var reason = Optional(request.DenialReason, 50);
        if (reason is not null && !SecurityAuditDenialReasons.All.Contains(reason))
            throw new SecurityAuditReviewValidationException("Denial reason is not approved.");
        var capability = Optional(request.Capability, 100);
        if (capability is not null && capability != SecurityAuditCapabilities.TenantSelection &&
            !SensitiveCapabilityCatalog.TryGetRequiredPermission(capability, out _))
            throw new SecurityAuditReviewValidationException("Capability is not approved.");
        var source = Optional(request.SourceApplication, 50);
        if (source is not null && source is not (SecurityAuditSourceApplications.Api or
            SecurityAuditSourceApplications.Web or SecurityAuditSourceApplications.Auth))
            throw new SecurityAuditReviewValidationException("Source application is not approved.");
        if (request.TargetTenantUid == Guid.Empty)
            throw new SecurityAuditReviewValidationException("Target tenant identifier is invalid.");
        return new SecurityAuditSearchCriteria(from, to, pageSize, null, null, reason, capability, source,
            request.TargetTenantUid, Optional(request.RequestCorrelationId, 128), Optional(request.ActorSubject, 450));
    }

    private SecurityAuditSearchCriteria WithContinuation(
        SecurityAuditSearchCriteria criteria, string? token, string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(token)) return criteria;
        if (token.Length > 2000 || !tokenProtector.TryUnprotect(token, out var cursor) ||
            cursor.SecurityAuditEventUid == Guid.Empty || cursor.OccurredAtUtc < criteria.FromUtc ||
            cursor.OccurredAtUtc >= criteria.ToUtc ||
            !string.Equals(cursor.FilterFingerprint, fingerprint, StringComparison.Ordinal))
            throw new SecurityAuditReviewValidationException("Continuation token is invalid.");
        return criteria with
        {
            CursorOccurredAtUtc = cursor.OccurredAtUtc.ToUniversalTime(),
            CursorSecurityAuditEventUid = cursor.SecurityAuditEventUid
        };
    }

    private static string Fingerprint(SecurityAuditSearchCriteria value)
    {
        var canonical = string.Join('|', value.FromUtc.ToString("O"), value.ToUtc.ToString("O"), value.PageSize,
            value.DenialReason, value.Capability, value.SourceApplication, value.TargetTenantUid,
            value.RequestCorrelationId, value.ActorSubject);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(canonical)));
    }

    private static string FilterSummary(SecurityAuditSearchCriteria value) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            fromUtc = value.FromUtc, toUtc = value.ToUtc,
            filters = new[] { value.DenialReason is null ? null : "denialReason",
                value.Capability is null ? null : "capability", value.SourceApplication is null ? null : "sourceApplication",
                value.TargetTenantUid is null ? null : "targetTenantUid",
                value.RequestCorrelationId is null ? null : "requestCorrelationId",
                value.ActorSubject is null ? null : "actorSubject" }.Where(x => x is not null)
        });

    private static string? Optional(string? value, int maximum)
    {
        if (value is null) return null;
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximum)
            throw new SecurityAuditReviewValidationException("A search filter is invalid.");
        return normalized;
    }

    private static string Required(string value, int maximum, string name)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > maximum)
            throw new SecurityAuditReviewValidationException($"{name} is invalid.");
        return normalized;
    }
}
