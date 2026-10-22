using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Hosting;
using MicroEMR.Application.OperationalTelemetry;

namespace MicroEMR.Web;

public sealed class SafeRequestTelemetryMiddleware(RequestDelegate next, ILogger<SafeRequestTelemetryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IHostEnvironment environment, IConfiguration configuration)
    {
        var timer = Stopwatch.StartNew();
        try { await next(context); }
        finally
        {
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
            var capture = RequestCorrelationDiagnostics.ShouldCapture(environment.IsDevelopment(),
                bool.TryParse(configuration[RequestCorrelationDiagnostics.EnabledKey], out var enabled) && enabled,
                "MicroEMR.Web", context.Request.Method, route, action?.ControllerName, action?.ActionName);
            logger.RequestCompleted("MicroEMR.Web", route, context.Request.Method,
                context.Response.StatusCode, timer.ElapsedMilliseconds, context.TraceIdentifier, capture);
        }
    }
}
