using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientTasks;

public sealed class PatientTaskResponse
{
    public Guid PatientTaskUid { get; set; }
    public Guid PatientUid { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string? TaskDescription { get; set; }
    public string TaskType { get; set; } = "General";
    public string TaskPriority { get; set; } = "Normal";
    public string TaskStatus { get; set; } = "Open";
    public DateTime? DueAt { get; set; }
    public long? AssignedTo { get; set; }
    public string? AssignedToDisplayName { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? CompletedBy { get; set; }
    public string? CompletedByDisplayName { get; set; }
    public string? CompletionNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public string? UpdatedByDisplayName { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class PatientDashboardTaskResponse
{
    public Guid PatientTaskUid { get; set; }
    public Guid PatientUid { get; set; }
    public string PatientDisplayName { get; set; } = string.Empty;
    public string? ChartNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string? TaskDescription { get; set; }
    public string TaskType { get; set; } = "General";
    public string TaskPriority { get; set; } = "Normal";
    public string TaskStatus { get; set; } = "Open";
    public DateTime? DueAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SavePatientTaskRequest
{
    [Required, StringLength(200)] public string TaskTitle { get; set; } = string.Empty;
    [StringLength(1000)] public string? TaskDescription { get; set; }
    [StringLength(50)] public string? TaskType { get; set; }
    [StringLength(50)] public string? TaskPriority { get; set; }
    public DateTime? DueAt { get; set; }
    public long? AssignedTo { get; set; }
}

public sealed class CreatePatientTaskRequest : SavePatientTaskRequest;
public sealed class UpdatePatientTaskRequest : SavePatientTaskRequest;
public sealed class CompletePatientTaskRequest
{
    [StringLength(1000)] public string? CompletionNote { get; set; }
}
