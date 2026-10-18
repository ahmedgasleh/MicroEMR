using MicroEMR.Application.PatientClinicalHistory;
using MicroEMR.Application.PatientProblems.Contracts;

namespace MicroEMR.Web.Models.PatientProblems;

public sealed class PatientProblemViewModel
{
    public Guid PatientProblemUid { get; set; }
    public Guid PatientUid { get; set; }
    public string ProblemName { get; set; } = string.Empty;
    public string? ProblemDescription { get; set; }
    public DateTime? OnsetDate { get; set; }
    public ProblemClinicalFields? ClinicalFields { get; set; }
    public ClinicalPartialDate? ClinicalResolutionDate { get; set; }
    public string OnsetDisplay => ClinicalFields?.OnsetDate?.Display ?? OnsetDate?.ToString("yyyy-MM-dd") ?? "Not recorded";
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
public class CreatePatientProblemRequest : MicroEMR.Application.PatientProblems.Contracts.CreatePatientProblemRequest;
public class UpdatePatientProblemRequest : MicroEMR.Application.PatientProblems.Contracts.UpdatePatientProblemRequest;
public class ResolvePatientProblemRequest : MicroEMR.Application.PatientProblems.Contracts.ResolvePatientProblemRequest;
public sealed class CreatePatientProblemViewModel : CreatePatientProblemRequest
{
    public Guid PatientUid { get; set; }
    public bool ClinicalFieldsIncluded { get; set; }
}
public sealed class UpdatePatientProblemViewModel : UpdatePatientProblemRequest
{
    public Guid PatientUid { get; set; }
    public Guid PatientProblemUid { get; set; }
    public bool ClinicalFieldsIncluded { get; set; }
}
public sealed class ResolvePatientProblemViewModel : ResolvePatientProblemRequest
{
    public Guid PatientUid { get; set; }
    public Guid PatientProblemUid { get; set; }
}
