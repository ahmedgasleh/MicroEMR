using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.OperationalTelemetry;
using System.Text.Json;

namespace MicroEMR.Api.Middleware;

public sealed class SafeApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<SafeApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted ||
            (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested))
            return false;

        var traceId = OperationalTrace.Capture(context.TraceIdentifier).TraceId;
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
        var sql = FindSqlException(exception);
        logger.LogError(
            "Operational event {EventCode}. Operation: {Operation}; ErrorCategory: {ErrorCategory}; HttpStatusCode: {HttpStatusCode}; SqlNumber: {SqlNumber}; SqlState: {SqlState}; TraceId: {TraceId}",
            OperationalEventCodes.UnexpectedApplicationError, route, exception.GetType().Name,
            StatusCodes.Status500InternalServerError, sql?.Number, sql?.State, traceId);

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "The request could not be completed. Contact support with the trace ID."
        };
        problem.Extensions["traceId"] = traceId;
        if (!await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem
        }))
        {
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(context.Response.Body, problem,
                cancellationToken: cancellationToken);
        }
        return true;
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
            if (current is SqlException sql)
                return sql;
        return null;
    }
}
