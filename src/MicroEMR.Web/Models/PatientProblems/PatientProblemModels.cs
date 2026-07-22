using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.PatientProblems;

public sealed class PatientProblemViewModel
{
    public Guid PatientProblemUid { get; set; }
    public Guid PatientUid { get; set; }
    public string ProblemName { get; set; } = string.Empty;
    public string? ProblemDescription { get; set; }
    public DateTime? OnsetDate { get; set; }
    public string ProblemStatus { get; set; } = string.Empty;
    public DateTime? ResolvedAt { get; set; }
    public long? ResolvedBy { get; set; }
    public string? ResolvedByDisplayName { get; set; }
    public string? ResolutionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CreatePatientProblemViewModel
{
    public Guid PatientUid { get; set; }
    [Required(ErrorMessage = "Problem name is required."), StringLength(200)] public string ProblemName { get; set; } = string.Empty;
    [StringLength(1000)] public string? ProblemDescription { get; set; }
    [DataType(DataType.Date)] public DateTime? OnsetDate { get; set; }
}

public sealed class UpdatePatientProblemViewModel
{
    public Guid PatientUid { get; set; }
    public Guid PatientProblemUid { get; set; }
    [Required(ErrorMessage = "Problem name is required."), StringLength(200)] public string ProblemName { get; set; } = string.Empty;
    [StringLength(1000)] public string? ProblemDescription { get; set; }
    [DataType(DataType.Date)] public DateTime? OnsetDate { get; set; }
}

public sealed class ResolvePatientProblemViewModel
{
    public Guid PatientUid { get; set; }
    public Guid PatientProblemUid { get; set; }
    [StringLength(500)] public string? ResolutionReason { get; set; }
}

public class CreatePatientProblemRequest
{
    public string ProblemName { get; set; } = string.Empty;
    public string? ProblemDescription { get; set; }
    public DateTime? OnsetDate { get; set; }
}
public sealed class UpdatePatientProblemRequest : CreatePatientProblemRequest;
public sealed class ResolvePatientProblemRequest { public string? ResolutionReason { get; set; } }
