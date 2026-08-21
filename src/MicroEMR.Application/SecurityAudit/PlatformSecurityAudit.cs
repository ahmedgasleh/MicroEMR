namespace MicroEMR.Application.SecurityAudit;

public static class SecurityAuditCapabilities
{
    public const string PatientChartView = "PatientChartView";
    public const string EncounterView = "EncounterView";
    public const string PatientDocumentView = "PatientDocumentView";
    public const string PatientFileDownload = "PatientFileDownload";
    public const string AppointmentReportRun = "AppointmentReportRun";
    public const string AppointmentReportExport = "AppointmentReportExport";
}

public static class SecurityAuditSourceApplications
{
    public const string Api = "MicroEMR.Api";
    public const string Web = "MicroEMR.Web";
}

public sealed record MissingPermissionSecurityEvent(
    string ActorSubject,
    long? ClinicalUserId,
    Guid? TrustedTenantUid,
    string Capability,
    string RequiredPermission,
    string SourceApplication,
    string? RequestCorrelationId);

public interface IPlatformSecurityAuditRepository
{
    Task RecordMissingPermissionAsync(
        MissingPermissionSecurityEvent securityEvent,
        CancellationToken cancellationToken = default);
}
