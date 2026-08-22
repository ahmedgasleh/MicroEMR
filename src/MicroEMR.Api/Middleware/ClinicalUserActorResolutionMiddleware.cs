using MicroEMR.Api.ClinicalUsers;
using MicroEMR.Application.AccessProfiles;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.SecurityAudit;
using MicroEMR.Application.Tenancy;
using System.Security.Claims;
using System.Text.Json;
using MicroEMR.Api.Authorization;

namespace MicroEMR.Api.Middleware;

public sealed class ClinicalUserActorResolutionMiddleware(
    RequestDelegate next,
    ILogger<ClinicalUserActorResolutionMiddleware> logger)
{
    private static readonly object RecordedUnresolvedCapabilitiesKey = new();

    public async Task InvokeAsync(
        HttpContext context,
        IAuthenticatedClinicalUserAccessor clinicalUserAccessor,
        ITenantContextAccessor tenantContextAccessor,
        IPlatformSecurityAuditRepository securityAudit)
    {
        if (!IsAuthenticatedMutation(context) ||
            context.GetEndpoint()?.Metadata.GetMetadata<RequirePlatformEntitlementAttribute>() is not null)
        {
            await next(context);
            return;
        }

        var tenantContext = tenantContextAccessor.Current
            ?? throw new InvalidOperationException("Tenant context has not been established for the clinical mutation.");

        try
        {
            var userId = await clinicalUserAccessor.GetRequiredUserIdAsync(context.RequestAborted);
            ClinicalUserActorContext.Set(context, userId);
            await next(context);
        }
        catch (ClinicalUserResolutionException exception)
        {
            if (exception.IsCompletedUnresolved)
                await TryRecordUnresolvedActorAsync(context, tenantContext, securityAudit);

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

    private async Task TryRecordUnresolvedActorAsync(
        HttpContext context,
        ITenantContext tenantContext,
        IPlatformSecurityAuditRepository securityAudit)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<SensitiveCapabilityAttribute>();
        if (metadata is null ||
            !string.Equals(metadata.Capability, SecurityAuditCapabilities.EncounterEdit, StringComparison.Ordinal) ||
            !SensitiveCapabilityCatalog.TryGetRequiredPermission(metadata.Capability, out var permission) ||
            !string.Equals(permission, PermissionKeys.EncountersEdit, StringComparison.Ordinal))
            return;

        var subject = context.User.FindFirstValue("sub")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject) || tenantContext.TenantUid == Guid.Empty ||
            !TryMarkFirstAttempt(context, metadata.Capability))
            return;

        try
        {
            await securityAudit.RecordUnresolvedClinicalActorAsync(
                new UnresolvedClinicalActorSecurityEvent(
                    subject,
                    tenantContext.TenantUid,
                    metadata.Capability,
                    permission,
                    SecurityAuditSourceApplications.Api,
                    context.TraceIdentifier),
                context.RequestAborted);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Unresolved-clinical-actor security audit persistence failed. Capability: {Capability}; Permission: {Permission}; TraceIdentifier: {TraceIdentifier}.",
                metadata.Capability, permission, context.TraceIdentifier);
        }
    }

    private static bool TryMarkFirstAttempt(HttpContext context, string capability)
    {
        if (!context.Items.TryGetValue(RecordedUnresolvedCapabilitiesKey, out var value) ||
            value is not HashSet<string> recordedCapabilities)
        {
            recordedCapabilities = new HashSet<string>(StringComparer.Ordinal);
            context.Items[RecordedUnresolvedCapabilitiesKey] = recordedCapabilities;
        }

        return recordedCapabilities.Add(capability);
    }

    private static bool IsAuthenticatedMutation(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true) return false;
        var method = context.Request.Method;
        return HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
    }
}
