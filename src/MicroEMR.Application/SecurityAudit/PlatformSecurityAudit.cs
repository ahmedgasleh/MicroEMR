namespace MicroEMR.Application.SecurityAudit;

using MicroEMR.Application.AccessProfiles;

public static class SecurityAuditCapabilities
{
    public const string PatientChartView = "PatientChartView";
    public const string EncounterView = "EncounterView";
    public const string EncounterEdit = "EncounterEdit";
    public const string PatientDocumentView = "PatientDocumentView";
    public const string PatientFileDownload = "PatientFileDownload";
    public const string AppointmentReportRun = "AppointmentReportRun";
    public const string AppointmentReportExport = "AppointmentReportExport";
}

public static class SensitiveCapabilityCatalog
{
    private static readonly IReadOnlyDictionary<string, string> PermissionByCapability =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SecurityAuditCapabilities.PatientChartView] = PermissionKeys.PatientsView,
            [SecurityAuditCapabilities.EncounterView] = PermissionKeys.EncountersView,
            [SecurityAuditCapabilities.PatientDocumentView] = PermissionKeys.DocumentsView,
            [SecurityAuditCapabilities.PatientFileDownload] = PermissionKeys.DocumentsView,
            [SecurityAuditCapabilities.AppointmentReportRun] = PermissionKeys.ReportsView,
            [SecurityAuditCapabilities.AppointmentReportExport] = PermissionKeys.ReportsExport
        };

    public static bool TryGetRequiredPermission(string capability, out string requiredPermission) =>
        PermissionByCapability.TryGetValue(capability, out requiredPermission!);
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class SensitiveCapabilityAttribute : Attribute
{
    public SensitiveCapabilityAttribute(string capability)
    {
        if (!SensitiveCapabilityCatalog.TryGetRequiredPermission(capability, out _))
            throw new ArgumentException("Unknown sensitive capability.", nameof(capability));

        Capability = capability;
    }

    public string Capability { get; }
}

public static class SecurityAuditSourceApplications
{
    public const string Api = "MicroEMR.Api";
    public const string Web = "MicroEMR.Web";
}

public static class SecurityAuditResourceTypes
{
    public const string Encounter = "Encounter";
}

public sealed record MissingPermissionSecurityEvent(
    string ActorSubject,
    long? ClinicalUserId,
    Guid? TrustedTenantUid,
    string Capability,
    string RequiredPermission,
    string SourceApplication,
    string? RequestCorrelationId);

public sealed record CrossPatientOwnershipSecurityEvent(
    string ActorSubject,
    long? ClinicalUserId,
    Guid TrustedTenantUid,
    string Capability,
    Guid RequestedPatientUid,
    Guid AuthoritativePatientUid,
    string ResourceType,
    Guid ResourceUid,
    string SourceApplication,
    string? RequestCorrelationId);

public sealed record UnresolvedClinicalActorSecurityEvent(
    string ActorSubject,
    Guid TrustedTenantUid,
    string Capability,
    string RequiredPermission,
    string SourceApplication,
    string? RequestCorrelationId);

public interface IPlatformSecurityAuditRepository
{
    Task RecordMissingPermissionAsync(
        MissingPermissionSecurityEvent securityEvent,
        CancellationToken cancellationToken = default);

    Task RecordCrossPatientOwnershipAsync(
        CrossPatientOwnershipSecurityEvent securityEvent,
        CancellationToken cancellationToken = default);

    Task RecordUnresolvedClinicalActorAsync(
        UnresolvedClinicalActorSecurityEvent securityEvent,
        CancellationToken cancellationToken = default);
}
