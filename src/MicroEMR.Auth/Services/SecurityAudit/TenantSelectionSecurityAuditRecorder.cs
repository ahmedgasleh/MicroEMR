using MicroEMR.Application.SecurityAudit;

namespace MicroEMR.Auth.Services.SecurityAudit;

public sealed class TenantSelectionSecurityAuditRecorder(
    IPlatformSecurityAuditRepository securityAudit,
    ILogger<TenantSelectionSecurityAuditRecorder> logger)
{
    private static readonly object RecordedKey = new();

    public async Task TryRecordInvalidMembershipAsync(
        HttpContext context,
        string actorSubject,
        Guid requestedTenantUid)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User.Identity?.IsAuthenticated != true ||
            string.IsNullOrWhiteSpace(actorSubject) ||
            requestedTenantUid == Guid.Empty ||
            context.Items.ContainsKey(RecordedKey))
            return;

        context.Items[RecordedKey] = true;

        try
        {
            await securityAudit.RecordInvalidTenantMembershipAsync(
                new InvalidTenantMembershipSecurityEvent(
                    actorSubject,
                    requestedTenantUid,
                    SecurityAuditSourceApplications.Auth,
                    context.TraceIdentifier),
                context.RequestAborted);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Invalid-tenant-membership security audit persistence failed. Capability: {Capability}; TraceIdentifier: {TraceIdentifier}.",
                SecurityAuditCapabilities.TenantSelection,
                context.TraceIdentifier);
        }
    }
}
