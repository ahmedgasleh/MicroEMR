using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MicroEMR.Application.OperationalTelemetry;

public static class OperationalEventCodes
{
    public const string HttpDependencyFailed = "HTTP_DEPENDENCY_FAILED";
    public const string TenantDatabaseUnavailable = "TENANT_DATABASE_UNAVAILABLE";
    public const string PlatformDatabaseUnavailable = "PLATFORM_DATABASE_UNAVAILABLE";
    public const string AuthTokenRefreshFailed = "AUTH_TOKEN_REFRESH_FAILED";
    public const string TenantResolutionFailed = "TENANT_RESOLUTION_FAILED";
    public const string FileStorageUnavailable = "FILE_STORAGE_UNAVAILABLE";
    public const string UnexpectedApplicationError = "UNEXPECTED_APPLICATION_ERROR";
    public const string HttpRequestCompleted = "HTTP_REQUEST_COMPLETED";
}

public readonly record struct OperationalTrace(string TraceId, string SpanId)
{
    public static OperationalTrace Capture(string? fallbackTraceIdentifier = null)
    {
        var activity = Activity.Current;
        if (activity is not null)
            return new(activity.TraceId.ToHexString(), activity.SpanId.ToHexString());

        return new(string.IsNullOrWhiteSpace(fallbackTraceIdentifier)
            ? "unavailable"
            : fallbackTraceIdentifier.Trim(), "unavailable");
    }
}

public static class SafeOperationalLog
{
    public static void HttpDependencyFailed(this ILogger logger, string operation,
        int statusCode, string? fallbackTraceIdentifier = null)
    {
        var trace = OperationalTrace.Capture(fallbackTraceIdentifier);
        logger.LogWarning(
            "Operational event {EventCode}. DependencyType: {DependencyType}; Operation: {Operation}; HttpStatusCode: {HttpStatusCode}; Outcome: {Outcome}; ErrorCategory: {ErrorCategory}; TraceId: {TraceId}; SpanId: {SpanId}",
            OperationalEventCodes.HttpDependencyFailed, "MicroEMR.Api", operation,
            statusCode, "Failed", "HttpDependencyFailure", trace.TraceId, trace.SpanId);
    }

    public static void SafeFailure(this ILogger logger, LogLevel level, string eventCode,
        string operation, string errorCategory, Guid? tenantUid = null,
        string? fallbackTraceIdentifier = null)
    {
        var trace = OperationalTrace.Capture(fallbackTraceIdentifier);
        logger.Log(level,
            "Operational event {EventCode}. Operation: {Operation}; Outcome: {Outcome}; ErrorCategory: {ErrorCategory}; TenantUid: {TenantUid}; TraceId: {TraceId}; SpanId: {SpanId}",
            eventCode, operation, "Failed", errorCategory, tenantUid, trace.TraceId, trace.SpanId);
    }

    public static void RequestCompleted(this ILogger logger, string service, string routeCategory,
        string method, int statusCode, long durationMs, string? fallbackTraceIdentifier = null)
    {
        var trace = OperationalTrace.Capture(fallbackTraceIdentifier);
        var level = statusCode >= 500 ? LogLevel.Error : statusCode >= 400 ? LogLevel.Warning : LogLevel.Debug;
        logger.Log(level,
            "Operational event {EventCode}. Service: {Service}; RouteCategory: {RouteCategory}; HttpMethod: {HttpMethod}; HttpStatusCode: {HttpStatusCode}; DurationMs: {DurationMs}; Outcome: {Outcome}; TraceId: {TraceId}; SpanId: {SpanId}",
            OperationalEventCodes.HttpRequestCompleted, service, routeCategory, method,
            statusCode, durationMs, statusCode < 400 ? "Succeeded" : "Failed", trace.TraceId, trace.SpanId);
    }
}
