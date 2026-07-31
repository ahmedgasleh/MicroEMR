using System.Text.Json;
using MicroEMR.Application.Tenancy;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Api.Middleware;

public sealed class TenantDatabaseExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantDatabaseExceptionMiddleware> _logger;

    public TenantDatabaseExceptionMiddleware(
        RequestDelegate next,
        ILogger<TenantDatabaseExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContextAccessor tenantContextAccessor)
    {
        try
        {
            await _next(context);
        }
        catch (TenantDatabaseConnectionException exception)
        {
            _logger.LogError(
                exception,
                "Tenant database operation rejected. TenantUid: {TenantUid}; Subject: {Subject}; Path: {Path}; TraceIdentifier: {TraceIdentifier}; Outcome: DatabaseUnavailable",
                tenantContextAccessor.Current?.TenantUid,
                context.User.FindFirst("sub")?.Value,
                context.Request.Path,
                context.TraceIdentifier);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                new
                {
                    type = "about:blank",
                    title = "Service Unavailable",
                    status = StatusCodes.Status503ServiceUnavailable,
                    detail = "The tenant data service is temporarily unavailable.",
                    traceId = context.TraceIdentifier
                },
                cancellationToken: context.RequestAborted);
        }
    }
}
