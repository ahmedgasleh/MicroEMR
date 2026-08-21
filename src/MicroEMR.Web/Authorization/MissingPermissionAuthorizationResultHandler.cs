using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.DependencyInjection;
using MicroEMR.Application.SecurityAudit;

namespace MicroEMR.Web.Authorization;

public sealed class MissingPermissionAuthorizationResultHandler(
    IServiceProvider services,
    ILogger<MissingPermissionAuthorizationResultHandler> logger)
    : IAuthorizationMiddlewareResultHandler
{
    private static readonly AuthorizationMiddlewareResultHandler DefaultHandler = new();
    private static readonly object RecordedCapabilitiesKey = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context,
        AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.User.Identity?.IsAuthenticated == true)
            await TryRecordAsync(context, policy);

        await DefaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }

    private async Task TryRecordAsync(HttpContext context, AuthorizationPolicy policy)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<SensitiveCapabilityAttribute>();
        if (metadata is null ||
            !SensitiveCapabilityCatalog.TryGetRequiredPermission(metadata.Capability, out var permission) ||
            !policy.Requirements.OfType<WebPermissionRequirement>()
                .Any(requirement => string.Equals(requirement.Key, permission, StringComparison.Ordinal)))
            return;

        var subject = context.User.FindFirstValue("sub")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject) || !TryMarkFirstAttempt(context, metadata.Capability)) return;

        try
        {
            var securityAudit = services.GetRequiredService<IPlatformSecurityAuditRepository>();
            await securityAudit.RecordMissingPermissionAsync(new MissingPermissionSecurityEvent(
                subject,
                ClinicalUserId: null,
                TrustedTenantUid: null,
                metadata.Capability,
                permission,
                SecurityAuditSourceApplications.Web,
                context.TraceIdentifier), context.RequestAborted);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Missing-permission security audit persistence failed. Capability: {Capability}; Permission: {Permission}; TraceIdentifier: {TraceIdentifier}.",
                metadata.Capability, permission, context.TraceIdentifier);
        }
    }

    private static bool TryMarkFirstAttempt(HttpContext context, string capability)
    {
        if (!context.Items.TryGetValue(RecordedCapabilitiesKey, out var value) ||
            value is not HashSet<string> recordedCapabilities)
        {
            recordedCapabilities = new HashSet<string>(StringComparer.Ordinal);
            context.Items[RecordedCapabilitiesKey] = recordedCapabilities;
        }

        return recordedCapabilities.Add(capability);
    }
}
