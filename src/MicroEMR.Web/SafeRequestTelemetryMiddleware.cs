using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using MicroEMR.Application.OperationalTelemetry;

namespace MicroEMR.Web;

public sealed class SafeRequestTelemetryMiddleware(RequestDelegate next, ILogger<SafeRequestTelemetryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var timer = Stopwatch.StartNew();
        try { await next(context); }
        finally
        {
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            logger.RequestCompleted("MicroEMR.Web", route, context.Request.Method,
                context.Response.StatusCode, timer.ElapsedMilliseconds, context.TraceIdentifier);
        }
    }
}
