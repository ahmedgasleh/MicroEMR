using System.Security.Claims;
using MicroEMR.Application.Security;
using MicroEMR.Application.Tenancy;
using MicroEMR.Core.Tenancy;
using System.Text.Json;
using MicroEMR.Api.Authorization;
using MicroEMR.Application.OperationalTelemetry;

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
            _logger.SafeFailure(LogLevel.Warning, OperationalEventCodes.TenantResolutionFailed,
                "Tenant.Resolve", "InvalidTenantClaim", fallbackTraceIdentifier: httpContext.TraceIdentifier);
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
                _logger.SafeFailure(LogLevel.Warning, OperationalEventCodes.TenantResolutionFailed,
                    "Tenant.Resolve", "TenantUnavailable", tenantUid, httpContext.TraceIdentifier);
                await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden, UnavailableMessage);
                return;
            }

            var membership = string.IsNullOrWhiteSpace(subject)
                ? null
                : await membershipRepository.GetMembershipAsync(
                    subject,
                    tenantUid,
                    httpContext.RequestAborted);

            if (membership is null ||
                !string.Equals(membership.UserId, subject, StringComparison.Ordinal) ||
                membership.TenantUid != tenantUid ||
                !string.Equals(membership.MembershipStatus, "Active", StringComparison.Ordinal))
            {
                _logger.SafeFailure(LogLevel.Warning, OperationalEventCodes.TenantResolutionFailed,
                    "Tenant.Resolve", "MembershipInactive", tenantUid, httpContext.TraceIdentifier);
                await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden, UnavailableMessage);
                return;
            }

            ReplaceTenantRoleClaims(httpContext.User, membership.Roles);

            tenantContextAccessor.SetTenant(new TenantContext(
                tenant.TenantUid,
                tenant.TenantKey,
                tenant.DisplayName));

            // Successful resolution is intentionally not logged per request; request telemetry
            // and governed audit events provide correlation without operational noise.
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _logger.SafeFailure(LogLevel.Error, OperationalEventCodes.PlatformDatabaseUnavailable,
                "Tenant.Resolve", "PlatformDatabaseUnavailable", tenantUid, httpContext.TraceIdentifier);
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
        context.GetEndpoint()?.Metadata.GetMetadata<RequirePlatformEntitlementAttribute>() is not null;

    private static void ReplaceTenantRoleClaims(
        ClaimsPrincipal principal,
        IReadOnlyCollection<string> currentRoles)
    {
        foreach (var identity in principal.Identities.OfType<ClaimsIdentity>())
        {
            foreach (var claim in identity.FindAll(MicroEmrClaimTypes.TenantRole).ToArray())
                identity.RemoveClaim(claim);
        }

        if (principal.Identity is not ClaimsIdentity authenticatedIdentity)
            return;

        foreach (var role in currentRoles
                     .Where(role => !string.IsNullOrWhiteSpace(role))
                     .Distinct(StringComparer.Ordinal))
        {
            authenticatedIdentity.AddClaim(new Claim(
                MicroEmrClaimTypes.TenantRole,
                role));
        }
    }

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
