using MicroEMR.Application.SecurityAudit;

namespace MicroEMR.Web.Models.SecurityAudit;

public sealed class SecurityAuditSearchForm
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? DenialReason { get; set; }
    public string? Capability { get; set; }
    public string? SourceApplication { get; set; }
    public Guid? TargetTenantUid { get; set; }
    public string? RequestCorrelationId { get; set; }
    public string? ActorSubject { get; set; }
    public string? ContinuationToken { get; set; }
}

public sealed class SecurityAuditIndexViewModel
{
    public required SecurityAuditSearchForm Filters { get; init; }
    public SecurityAuditSearchPage? Results { get; init; }
    public string? PagingStateToken { get; init; }
    public bool ActorSubjectFilterApplied { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsValidationError { get; init; }
    public IReadOnlyList<string> DenialReasons =>
        ["MissingPermission", "CrossPatientOwnership", "UnresolvedClinicalActor", "InvalidTenantMembership"];
    public IReadOnlyList<string> Capabilities =>
        ["PatientChartView", "EncounterView", "EncounterEdit", "TenantSelection", "PatientDocumentView",
         "PatientFileDownload", "AppointmentReportRun", "AppointmentReportExport"];
    public IReadOnlyList<string> SourceApplications =>
        ["MicroEMR.Api", "MicroEMR.Web", "MicroEMR.Auth"];
}

public sealed class SecurityAuditDetailViewModel
{
    public SecurityAuditDetail? Detail { get; init; }
    public string? ErrorMessage { get; init; }
}
