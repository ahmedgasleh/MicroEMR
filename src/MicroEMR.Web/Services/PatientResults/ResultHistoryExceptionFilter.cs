using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MicroEMR.Application.OperationalTelemetry;

namespace MicroEMR.Web.Services.PatientResults;

// Bound to the history action: downstream failures must not reach DeveloperExceptionPage.
public sealed class ResultHistoryExceptionFilter(ILogger<ResultHistoryExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.HttpContext.Response.HasStarted ||
            context.Exception is OperationCanceledException && context.HttpContext.RequestAborted.IsCancellationRequested)
            return;

        var status = context.Exception is HttpRequestException { StatusCode: { } code } &&
            code is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.NotFound
            ? (int)code : StatusCodes.Status500InternalServerError;
        var traceId = OperationalTrace.Capture(context.HttpContext.TraceIdentifier).TraceId;
        logger.LogError(
            "Operational event {EventCode}. Operation: {Operation}; ErrorCategory: {ErrorCategory}; HttpStatusCode: {HttpStatusCode}; TraceId: {TraceId}",
            OperationalEventCodes.UnexpectedApplicationError, "ResultHistory", context.Exception.GetType().Name, status, traceId);

        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = status == 500 ? "An unexpected error occurred." : "The request could not be completed.",
            Status = status,
            Detail = "The request could not be completed. Contact support with the trace ID."
        };
        problem.Extensions["traceId"] = traceId;
        context.Result = new JsonResult(problem)
        {
            StatusCode = status,
            ContentType = "application/problem+json"
        };
        context.ExceptionHandled = true;
    }
}
