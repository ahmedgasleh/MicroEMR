using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MicroEMR.Application.Security;
using MicroEMR.Application.Tenancy;
using MicroEMR.Core.Tenancy;
using System.Text.Json;

namespace MicroEMR.Api.Middleware;

public sealed class TenantResolutionMiddleware
{
    private const string UnassignedMessage =
        "The access token is not assigned to an active tenant.";
    private const string UnavailableMessage =
        "The tenant for this request is unavailable or inactive.";

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        ITenantCatalog tenantCatalog,
        IUserTenantMembershipRepository membershipRepository,
        ITenantContextAccessor tenantContextAccessor)
    {
        if (ShouldSkip(httpContext))
        {
            await _next(httpContext);
            return;
        }

        var subject = httpContext.User.FindFirstValue("sub")
            ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tenantClaims = httpContext.User
            .FindAll(MicroEmrClaimTypes.TenantId)
            .Select(claim => claim.Value)
            .ToArray();

        if (tenantClaims.Length != 1 ||
            !Guid.TryParse(tenantClaims[0], out var tenantUid) ||
            tenantUid == Guid.Empty)
        {
            _logger.LogWarning(
                "Tenant claim validation failed. Subject: {Subject}; TraceIdentifier: {TraceIdentifier}; Path: {Path}; Outcome: InvalidTenantClaim",
                subject,
                httpContext.TraceIdentifier,
                httpContext.Request.Path);
            await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden, UnassignedMessage);
            return;
        }

        try
        {
            var tenant = await tenantCatalog.GetByUidAsync(
                tenantUid,
                httpContext.RequestAborted);

            if (tenant is null ||
                tenant.TenantUid != tenantUid ||
                tenant.Status != TenantStatus.Active ||
                string.IsNullOrWhiteSpace(tenant.TenantKey) ||
                string.IsNullOrWhiteSpace(tenant.DisplayName))
            {
                _logger.LogWarning(
                    "Tenant is unavailable or inactive. TenantUid: {TenantUid}; Subject: {Subject}; TraceIdentifier: {TraceIdentifier}; Path: {Path}; Outcome: TenantUnavailable",
                    tenantUid,
                    subject,
                    httpContext.TraceIdentifier,
                    httpContext.Request.Path);
                await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden, UnavailableMessage);
                return;
            }

            if (string.IsNullOrWhiteSpace(subject) ||
                await membershipRepository.GetMembershipAsync(
                    subject,
                    tenantUid,
                    httpContext.RequestAborted) is null)
            {
                _logger.LogWarning(
                    "Active tenant membership was not found. TenantUid: {TenantUid}; Subject: {Subject}; TraceIdentifier: {TraceIdentifier}; Path: {Path}; Outcome: MembershipInactive",
                    tenantUid,
                    subject,
                    httpContext.TraceIdentifier,
                    httpContext.Request.Path);
                await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden, UnavailableMessage);
                return;
            }

            tenantContextAccessor.SetTenant(new TenantContext(
                tenant.TenantUid,
                tenant.TenantKey,
                tenant.DisplayName));

            _logger.LogInformation(
                "Tenant context established. TenantUid: {TenantUid}; Subject: {Subject}; TraceIdentifier: {TraceIdentifier}; Path: {Path}; Outcome: Resolved",
                tenantUid,
                subject,
                httpContext.TraceIdentifier,
                httpContext.Request.Path);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Platform tenant resolution failed. TenantUid: {TenantUid}; Subject: {Subject}; TraceIdentifier: {TraceIdentifier}; Path: {Path}; Outcome: PlatformUnavailable",
                tenantUid,
                subject,
                httpContext.TraceIdentifier,
                httpContext.Request.Path);
            await WriteProblemAsync(
                httpContext,
                StatusCodes.Status503ServiceUnavailable,
                "Tenant validation is temporarily unavailable.");
            return;
        }

        try
        {
            await _next(httpContext);
        }
        finally
        {
            tenantContextAccessor.Clear();
        }
    }

    private static bool ShouldSkip(HttpContext context) =>
        context.User.Identity?.IsAuthenticated != true ||
        context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null;

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            new
            {
                type = "about:blank",
                title = statusCode == StatusCodes.Status403Forbidden
                    ? "Forbidden"
                    : "Service Unavailable",
                status = statusCode,
                detail,
                traceId = context.TraceIdentifier
            },
            cancellationToken: context.RequestAborted);
    }
}
