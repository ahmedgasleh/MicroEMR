using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using MicroEMR.Application.OperationalTelemetry;

namespace MicroEMR.Api.Middleware;

public sealed class SafeRequestTelemetryMiddleware(RequestDelegate next, ILogger<SafeRequestTelemetryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IHostEnvironment environment, IConfiguration configuration)
    {
        var timer = Stopwatch.StartNew();
        try { await next(context); }
        finally
        {
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            var capture = RequestCorrelationDiagnostics.ShouldCapture(environment.IsDevelopment(),
                bool.TryParse(configuration[RequestCorrelationDiagnostics.EnabledKey], out var enabled) && enabled,
                "MicroEMR.Api", context.Request.Method, route);
            logger.RequestCompleted("MicroEMR.Api", route, context.Request.Method,
                context.Response.StatusCode, timer.ElapsedMilliseconds, context.TraceIdentifier, capture);
        }
    }
}
