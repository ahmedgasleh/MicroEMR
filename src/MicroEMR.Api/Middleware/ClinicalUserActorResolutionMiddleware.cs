using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.Tenancy;
using System.Text.Json;

namespace MicroEMR.Api.Middleware;

public sealed class ClinicalUserActorResolutionMiddleware(
    RequestDelegate next,
    ILogger<ClinicalUserActorResolutionMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        IAuthenticatedClinicalUserAccessor clinicalUserAccessor,
        ITenantContext tenantContext)
    {
        if (!IsAuthenticatedMutation(context))
        {
            await next(context);
            return;
        }

        try
        {
            var userId = await clinicalUserAccessor.GetRequiredUserIdAsync(context.RequestAborted);
            ClinicalUserActorContext.Set(context, userId);
            await next(context);
        }
        catch (ClinicalUserResolutionException exception)
        {
            logger.LogWarning(
                "Clinical mutation actor resolution rejected. TenantUid: {TenantUid}; Path: {Path}; TraceIdentifier: {TraceIdentifier}; Reason: {Reason}",
                tenantContext.TenantUid, context.Request.Path, context.TraceIdentifier, exception.Message);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(context.Response.Body, new
            {
                type = "about:blank",
                title = "Clinical user access required",
                status = StatusCodes.Status403Forbidden,
                detail = "Your authenticated account is not provisioned for clinical changes in this tenant."
            }, cancellationToken: context.RequestAborted);
        }
    }

    private static bool IsAuthenticatedMutation(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true) return false;
        var method = context.Request.Method;
        return HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
    }
}
