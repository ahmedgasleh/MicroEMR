using System.ComponentModel.DataAnnotations;
using MicroEMR.Application.PatientProblems;
using MicroEMR.Application.PatientProblems.Contracts;
using MicroEMR.Application.PatientProblems.Repositories;

namespace MicroEMR.Application.PatientProblems.Services;

public sealed class PatientProblemService(IPatientProblemRepository repository) : IPatientProblemService
{
    public Task<IReadOnlyList<PatientProblemResponse>> GetByPatientUidAsync(Guid patientUid, string statusFilter, CancellationToken cancellationToken = default)
        => repository.GetByPatientUidAsync(patientUid, NormalizeStatus(statusFilter), cancellationToken);
    public Task<PatientProblemResponse?> GetByUidAsync(Guid patientUid, Guid patientProblemUid, CancellationToken cancellationToken = default)
        => repository.GetByUidAsync(patientUid, patientProblemUid, cancellationToken);
    public Task<PatientProblemResponse> CreateAsync(Guid patientUid, CreatePatientProblemRequest request, long? createdBy, CancellationToken cancellationToken = default)
        { Validate(request); return repository.CreateAsync(patientUid, request, createdBy, cancellationToken); }
    public Task<PatientProblemResponse?> UpdateAsync(Guid patientUid, Guid patientProblemUid, UpdatePatientProblemRequest request, long? updatedBy, CancellationToken cancellationToken = default)
        { Validate(request); return repository.UpdateAsync(patientUid, patientProblemUid, request, updatedBy, cancellationToken); }
    public Task<PatientProblemResponse?> ResolveAsync(Guid patientUid, Guid patientProblemUid, ResolvePatientProblemRequest request, long? resolvedBy, CancellationToken cancellationToken = default)
        { Validate(request); return repository.ResolveAsync(patientUid, patientProblemUid, request, resolvedBy, cancellationToken); }

    private static void Validate(object request)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new(request), errors, true))
            throw new PatientProblemValidationException(errors[0].ErrorMessage ?? "Problem information is invalid.");
    }

    private static string NormalizeStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "resolved" => "Resolved",
        "all" => "All",
        _ => "Active"
    };
}
