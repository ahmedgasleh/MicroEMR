using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MicroEMR.Application.PatientChartAlerts;

public sealed class PatientChartAlertResponse
{
    public Guid PatientChartAlertUid { get; set; }
    public Guid PatientUid { get; set; }
    public string AlertTitle { get; set; } = "";
    public string? AlertMessage { get; set; }
    public string AlertType { get; set; } = "General";
    public string AlertPriority { get; set; } = "Normal";
    public string AlertStatus { get; set; } = "Active";
    public DateTime? ResolvedAt { get; set; }
    public long? ResolvedBy { get; set; }
    public string? ResolvedByDisplayName { get; set; }
    public string? ResolutionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public string? UpdatedByDisplayName { get; set; }
    public string? RowVersion { get; set; }
}
public class SavePatientChartAlertRequest
{
    [Required, StringLength(200)] public string AlertTitle { get; set; } = "";
    [StringLength(1000)] public string? AlertMessage { get; set; }
    [StringLength(50)] public string? AlertType { get; set; }
    [StringLength(50)] public string? AlertPriority { get; set; }
    [JsonIgnore] public string? RequestCorrelationId { get; set; }
}
public sealed class CreatePatientChartAlertRequest : SavePatientChartAlertRequest;
public sealed class UpdatePatientChartAlertRequest : SavePatientChartAlertRequest
{
    [Required] public string RowVersion { get; set; } = "";
}
public sealed class ResolvePatientChartAlertRequest
{
    [Required] public string RowVersion { get; set; } = "";
    [StringLength(500)] public string? ResolutionReason { get; set; }
    [JsonIgnore] public string? RequestCorrelationId { get; set; }
}
public sealed class ChartAlertValidationException(string message) : Exception(message);
public sealed class ChartAlertConcurrencyException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class ChartAlertNotFoundException : Exception;
