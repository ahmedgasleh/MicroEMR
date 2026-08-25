using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.OperationalTelemetry;
using MicroEMR.Web.Services;
using ApiTelemetryMiddleware = MicroEMR.Api.Middleware.SafeRequestTelemetryMiddleware;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class SafeOperationalTelemetryTests
{
    private static readonly string[] Sentinels =
    [
        "TEST_PATIENT_NAME_DO_NOT_LOG",
        "TEST_HEALTH_CARD_DO_NOT_LOG",
        "TEST_CLINICAL_NOTE_DO_NOT_LOG",
        "TEST_ACCESS_TOKEN_DO_NOT_LOG",
        "TEST_CLIENT_SECRET_DO_NOT_LOG"
    ];

    [Fact]
    public void DependencyFailureUsesControlledFieldsAndW3cTraceWithoutSensitiveBody()
    {
        using var activity = new Activity("WebToApi")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        var logger = new RecordingLogger();

        logger.HttpDependencyFailed("PatientApi.Get", 503);

        var message = Assert.Single(logger.Messages);
        Assert.Contains(OperationalEventCodes.HttpDependencyFailed, message);
        Assert.Contains("PatientApi.Get", message);
        Assert.Contains("503", message);
        Assert.Contains(activity.TraceId.ToHexString(), message);
        Assert.Contains(activity.SpanId.ToHexString(), message);
        Assert.DoesNotContain("?", message);
        foreach (var sentinel in Sentinels) Assert.DoesNotContain(sentinel, message);
    }

    [Fact]
    public void SafeApiExceptionSeparatesUiValidationBodyFromLogSafeMessage()
    {
        var body = string.Join('|', Sentinels) + " Password=fake;Server=private;Bearer fake-token";
        var exception = new SafeApiResponseException(System.Net.HttpStatusCode.BadRequest, body);

        Assert.Equal(body, SafeApiResponseException.ValidationBody(exception));
        Assert.DoesNotContain(body, exception.Message);
        foreach (var sentinel in Sentinels) Assert.DoesNotContain(sentinel, exception.Message);
        Assert.DoesNotContain("Server=private", exception.Message);
        Assert.DoesNotContain("fake-token", exception.Message);
    }

    [Fact]
    public async Task RequestTelemetryLogsTemplateNotRawPathQueryOrBody()
    {
        using var activity = new Activity("IncomingApi")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        var logger = new RecordingLogger<ApiTelemetryMiddleware>();
        var middleware = new ApiTelemetryMiddleware(context =>
        {
            context.Response.StatusCode = 503;
            return Task.CompletedTask;
        }, logger);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/patients/11111111-1111-1111-1111-111111111111/results";
        context.Request.QueryString = new QueryString("?note=TEST_CLINICAL_NOTE_DO_NOT_LOG");
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask,
            RoutePatternFactory.Parse("api/patients/{patientUid:guid}/results"), 0,
            EndpointMetadataCollection.Empty, "PatientResults.Create"));

        await middleware.InvokeAsync(context);

        var message = Assert.Single(logger.Messages);
        Assert.Contains("api/patients/{patientUid:guid}/results", message);
        Assert.Contains("POST", message);
        Assert.Contains("503", message);
        Assert.Contains(activity.TraceId.ToHexString(), message);
        Assert.DoesNotContain("11111111-1111-1111-1111-111111111111", message);
        foreach (var sentinel in Sentinels) Assert.DoesNotContain(sentinel, message);
    }

    [Fact]
    public void TenantFailureLogsOnlyOpaqueTenantIdentifier()
    {
        var logger = new RecordingLogger();
        var tenantUid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        logger.SafeFailure(LogLevel.Error, OperationalEventCodes.TenantDatabaseUnavailable,
            "TenantDatabase.Open", "DatabaseUnavailable", tenantUid, "fallback-trace");

        var message = Assert.Single(logger.Messages);
        Assert.Contains(tenantUid.ToString(), message);
        Assert.DoesNotContain("Tenant Display Name", message);
        Assert.DoesNotContain("PrivateDatabase", message);
        Assert.DoesNotContain("secret-reference", message);
        Assert.DoesNotContain("Server=", message);
    }

    [Fact]
    public void StandardW3cChildSpanPreservesTraceAndUsesDifferentSpan()
    {
        using var parent = new Activity("Web.Outbound")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        var parentTrace = OperationalTrace.Capture();
        using var child = new Activity("Api.Inbound")
            .SetIdFormat(ActivityIdFormat.W3C)
            .SetParentId(parent.Id!)
            .Start();
        var childTrace = OperationalTrace.Capture();

        Assert.Equal(parentTrace.TraceId, childTrace.TraceId);
        Assert.NotEqual(parentTrace.SpanId, childTrace.SpanId);
    }

    [Fact]
    public void AuthenticationFailureUsesControlledTelemetryWithoutExceptionDetail()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Web", "Authentication",
            "OpenIdConnectRefreshTokenClient.cs"));

        Assert.Contains(nameof(OperationalEventCodes.AuthTokenRefreshFailed), source);
        Assert.DoesNotContain("LogWarning(exception", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessToken}", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshToken}", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ClientSecret}", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WebApiClientsDoNotLogFailedResponseBodies()
    {
        var root = FindRepositoryRoot();
        var web = Path.Combine(root, "src", "MicroEMR.Web");
        var source = string.Join('\n', Directory.EnumerateFiles(web, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

        Assert.DoesNotContain("Response: {ResponseBody}", source, StringComparison.Ordinal);
        Assert.DoesNotContain("{ResponseBody}\"", source, StringComparison.Ordinal);
        foreach (var line in source.Split('\n').Where(x => x.Contains("Log", StringComparison.Ordinal)))
            Assert.DoesNotContain("responseBody", line, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MicroEMR.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class RecordingLogger<T> : RecordingLogger, ILogger<T>
    {
    }
}
