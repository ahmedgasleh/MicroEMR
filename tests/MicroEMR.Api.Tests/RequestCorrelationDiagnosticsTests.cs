using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.OperationalTelemetry;
using MicroEMR.Application.ReadAudit;
using ApiMiddleware = MicroEMR.Api.Middleware.SafeRequestTelemetryMiddleware;
using WebMiddleware = MicroEMR.Web.SafeRequestTelemetryMiddleware;
using Xunit;

namespace MicroEMR.Api.Tests;

public sealed class RequestCorrelationDiagnosticsTests
{
    private const string CppRoute = "api/patients/{patientUid:guid}/cpp";

    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task CompletionPreservesHttpIdentityAlongsideActivityAndFallback(bool web, bool hasActivity)
    {
        var previous = Activity.Current;
        Activity.Current = null;
        try
        {
            using var activity = hasActivity ? new Activity("Incoming").SetIdFormat(ActivityIdFormat.W3C).Start() : null;
            var provider = new RecordingProvider();
            using var factory = Factory(provider);
            var context = Context(web, "server-request-1");

            await Invoke(web, context, _ => Task.CompletedTask, factory, "Development", Configuration("true"));

            var entry = Assert.Single(provider.Entries);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal("server-request-1", entry.Fields["RequestId"]);
            Assert.Equal(activity?.TraceId.ToHexString() ?? "server-request-1", entry.Fields["TraceId"]);
            Assert.Equal(activity?.SpanId.ToHexString() ?? "unavailable", entry.Fields["SpanId"]);
            Assert.Equal(OperationalEventCodes.HttpRequestCompleted, entry.Fields["EventCode"]);
            Assert.Equal(web ? "MicroEMR.Web" : "MicroEMR.Api", entry.Fields["Service"]);
            Assert.Equal("GET", entry.Fields["HttpMethod"]);
            Assert.Equal(200, entry.Fields["HttpStatusCode"]);
            Assert.Equal("Succeeded", entry.Fields["Outcome"]);
            Assert.IsType<long>(entry.Fields["DurationMs"]);
            Assert.Equal("server-request-1", context.TraceIdentifier);
        }
        finally { Activity.Current = previous; }
    }

    [Theory]
    [InlineData(false, "Development", "true", true)]
    [InlineData(true, "Development", "true", true)]
    [InlineData(false, "Development", null, false)]
    [InlineData(true, "Development", null, false)]
    [InlineData(false, "Development", "false", false)]
    [InlineData(true, "Development", "false", false)]
    [InlineData(false, "Development", "invalid", false)]
    [InlineData(true, "Development", "invalid", false)]
    [InlineData(false, "Production", "true", false)]
    [InlineData(true, "Production", "true", false)]
    [InlineData(false, "Staging", "true", false)]
    [InlineData(true, "Staging", "true", false)]
    public async Task InformationLevelCaptureRequiresDevelopmentAndExplicitValidOptIn(
        bool web, string environment, string? optIn, bool visible)
    {
        var provider = new RecordingProvider();
        using var factory = Factory(provider, LogLevel.Information);

        await Invoke(web, Context(web, "request-gate"), _ => Task.CompletedTask,
            factory, environment, Configuration(optIn));

        Assert.Equal(visible ? 1 : 0, provider.Entries.Count);
    }

    [Theory]
    [InlineData("api/patients/{patientUid:guid}/cpp")]
    [InlineData("api/patients/{patientUid:guid}/allergies/snapshot")]
    [InlineData("api/patients/{patientUid:guid}/prescriptions")]
    [InlineData("api/patients/{patientUid:guid}/chart-alerts")]
    public async Task EachAuditedApiGetCanBeCapturedWithoutGlobalDebug(string route)
    {
        var provider = new RecordingProvider();
        using var factory = Factory(provider, LogLevel.Information);
        var context = Context(false, "audited-get", route);

        await Invoke(false, context, _ => Task.CompletedTask, factory, "Development", Configuration("true"));

        Assert.Equal(route, Assert.Single(provider.Entries).Fields["RouteCategory"]);
    }

    [Theory]
    [InlineData("Patients", "Details", true)]
    [InlineData("PatientChartAlerts", "List", true)]
    [InlineData("Patients", "Search", false)]
    [InlineData("Home", "Index", false)]
    public async Task WebCaptureUsesActionMetadataRatherThanTheSharedConventionalRoute(
        string controller, string action, bool visible)
    {
        var provider = new RecordingProvider();
        using var factory = Factory(provider, LogLevel.Information);
        var context = Context(true, "web-action", controller: controller, action: action);

        await Invoke(true, context, _ => Task.CompletedTask, factory, "Development", Configuration("true"));

        Assert.Equal(visible ? 1 : 0, provider.Entries.Count);
    }

    [Theory]
    [InlineData("POST", CppRoute)]
    [InlineData("GET", "api/patients/{patientUid:guid}/prescriptions/{prescriptionUid:guid}/private-note")]
    [InlineData("GET", "health/platform")]
    [InlineData("GET", null)]
    public async Task OptInDoesNotPromoteOtherRoutesOrMethods(string method, string? route)
    {
        var provider = new RecordingProvider();
        using var factory = Factory(provider, LogLevel.Information);
        var context = Context(false, "excluded");
        context.Request.Method = method;
        context.SetEndpoint(route is null ? null : Endpoint(route));

        await Invoke(false, context, _ => Task.CompletedTask, factory, "Development", Configuration("true"));

        Assert.Empty(provider.Entries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfigurationCanStopCaptureForTheNextRequest(bool web)
    {
        var provider = new RecordingProvider();
        using var factory = Factory(provider, LogLevel.Information);
        var configuration = Configuration("true");
        await Invoke(web, Context(web, "enabled"), _ => Task.CompletedTask, factory, "Development", configuration);

        configuration[RequestCorrelationDiagnostics.EnabledKey] = "false";
        await Invoke(web, Context(web, "disabled"), _ => Task.CompletedTask, factory, "Development", configuration);

        Assert.Equal("enabled", Assert.Single(provider.Entries).Fields["RequestId"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentRequestsKeepTheirOwnIds(bool web)
    {
        var provider = new RecordingProvider();
        using var factory = Factory(provider);
        var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        async Task Next(HttpContext _)
        {
            if (Interlocked.Increment(ref entered) == 2) bothEntered.SetResult();
            await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        var configuration = Configuration("true");

        await Task.WhenAll(
            Invoke(web, Context(web, "concurrent-1"), Next, factory, "Development", configuration),
            Invoke(web, Context(web, "concurrent-2"), Next, factory, "Development", configuration));

        Assert.Equal(2, provider.Entries.Count);
        Assert.Equal(new[] { "concurrent-1", "concurrent-2" },
            provider.Entries.Select(x => (string)x.Fields["RequestId"]!).Order().ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UntrustedHeadersAndClinicalContentNeverEnterCompletionFields(bool web)
    {
        const string sensitive = "SECRET_PATIENT_NOTE_COOKIE_TOKEN";
        var provider = new RecordingProvider();
        using var factory = Factory(provider);
        var context = Context(web, "trusted-server-id");
        context.Request.Headers.Authorization = "Bearer " + sensitive;
        context.Request.Headers.Cookie = "session=" + sensitive;
        context.Request.Headers["X-Request-ID"] = sensitive;
        context.Request.Headers["X-Correlation-ID"] = sensitive;
        context.Request.Headers["traceparent"] = sensitive;
        context.Request.QueryString = new QueryString("?patientName=" + sensitive);
        context.Request.Path = "/patients/11111111-1111-1111-1111-111111111111/" + sensitive;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(sensitive));
        context.Response.Body = new MemoryStream(Encoding.UTF8.GetBytes(sensitive));

        await Invoke(web, context, _ => Task.CompletedTask, factory, "Development", Configuration("true"));

        var entry = Assert.Single(provider.Entries);
        Assert.Equal("trusted-server-id", entry.Fields["RequestId"]);
        Assert.DoesNotContain(sensitive, entry.Message);
        Assert.DoesNotContain("11111111-1111-1111-1111-111111111111", entry.Message);
        Assert.DoesNotContain("?patientName=", entry.Message);
        Assert.Equal(((RouteEndpoint)context.GetEndpoint()!).RoutePattern.RawText, entry.Fields["RouteCategory"]);
        Assert.Equal("trusted-server-id", context.TraceIdentifier);
    }

    [Theory]
    [InlineData(200, LogLevel.Debug)]
    [InlineData(302, LogLevel.Debug)]
    [InlineData(403, LogLevel.Warning)]
    [InlineData(503, LogLevel.Error)]
    public async Task ProductionOptInDoesNotChangeExistingLevels(int status, LogLevel expected)
    {
        var provider = new RecordingProvider();
        using var factory = Factory(provider);
        var context = Context(false, "production-request");
        context.Response.StatusCode = status;

        await Invoke(false, context, _ => Task.CompletedTask, factory, "Production", Configuration("true"));

        Assert.Equal(expected, Assert.Single(provider.Entries).Level);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public async Task DiagnosticsPreserveExistingAuditCorrelationAndDoNotAddAuditWrites(string enabled)
    {
        var provider = new RecordingProvider();
        using var factory = Factory(provider);
        var repository = new AuditRepository();
        var audit = new PatientChartReadAuditService(repository, new Actor());
        var patient = Guid.NewGuid();
        var context = Context(false, "audit-request");

        await Invoke(false, context, async current =>
        {
            await audit.RecordOpenedAsync(patient, current.TraceIdentifier);
        }, factory, "Development", Configuration(enabled));

        Assert.Equal(1, repository.Calls);
        Assert.Equal("audit-request", repository.Correlation);
        Assert.Equal(patient, repository.Patient);
        Assert.Equal(73, repository.User);
        Assert.Equal("MicroEMR.Api", repository.Source);
        Assert.Equal(repository.Correlation, Assert.Single(provider.Entries).Fields["RequestId"]);
    }

    private static IConfigurationRoot Configuration(string? enabled) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [RequestCorrelationDiagnostics.EnabledKey] = enabled })
        .Build();

    private static ILoggerFactory Factory(RecordingProvider provider, LogLevel minimum = LogLevel.Debug) =>
        LoggerFactory.Create(builder => builder.SetMinimumLevel(minimum).AddProvider(provider));

    private static Task Invoke(bool web, HttpContext context, RequestDelegate next, ILoggerFactory factory,
        string environment, IConfiguration configuration) => web
        ? new WebMiddleware(next, factory.CreateLogger<WebMiddleware>()).InvokeAsync(context,
            new DiagnosticTestEnvironment(environment), configuration)
        : new ApiMiddleware(next, factory.CreateLogger<ApiMiddleware>()).InvokeAsync(context,
            new DiagnosticTestEnvironment(environment), configuration);

    private static DefaultHttpContext Context(bool web, string requestId, string route = CppRoute,
        string controller = "Patients", string action = "Details")
    {
        var context = new DefaultHttpContext { TraceIdentifier = requestId };
        context.Request.Method = "GET";
        context.SetEndpoint(web
            ? Endpoint("{controller=Home}/{action=Index}/{id?}", new ControllerActionDescriptor
                { ControllerName = controller, ActionName = action })
            : Endpoint(route));
        return context;
    }

    private static RouteEndpoint Endpoint(string route, params object[] metadata) => new(
        _ => Task.CompletedTask, RoutePatternFactory.Parse(route), 0,
        new EndpointMetadataCollection(metadata), "test-endpoint");

    private sealed record Entry(LogLevel Level, IReadOnlyDictionary<string, object?> Fields, string Message);

    private sealed class RecordingProvider : ILoggerProvider
    {
        public ConcurrentQueue<Entry> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Logger(Entries);
        public void Dispose() { }

        private sealed class Logger(ConcurrentQueue<Entry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => entries.Enqueue(new Entry(level,
                    ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(x => x.Key, x => x.Value),
                    formatter(state, exception)));
        }
    }

    private sealed class Actor : IAuthenticatedClinicalUserAccessor
    {
        public Task<long> GetRequiredUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(73L);
    }

    private sealed class AuditRepository : IReadAuditRepository
    {
        public int Calls { get; private set; }
        public string? Correlation { get; private set; }
        public Guid Patient { get; private set; }
        public long User { get; private set; }
        public string? Source { get; private set; }
        public Task<Guid> RecordPatientChartOpenedAsync(Guid patientUid, long clinicalUserId,
            string requestCorrelationId, string sourceApplication, CancellationToken cancellationToken = default)
        {
            Calls++;
            (Patient, User, Correlation, Source) = (patientUid, clinicalUserId, requestCorrelationId, sourceApplication);
            return Task.FromResult(Guid.NewGuid());
        }
        public Task<Guid> RecordStructuredReadAsync(string eventType, string resourceType, Guid resourceUid,
            Guid patientUid, long clinicalUserId, string requestCorrelationId, string sourceApplication,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid> RecordAggregateReportAsync(string eventType, string reportKey, long clinicalUserId,
            string requestCorrelationId, string sourceApplication, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

internal sealed class DiagnosticTestEnvironment(string name) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;
    public string ApplicationName { get; set; } = "MicroEMR.Tests";
    public string ContentRootPath { get; set; } = ".";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
