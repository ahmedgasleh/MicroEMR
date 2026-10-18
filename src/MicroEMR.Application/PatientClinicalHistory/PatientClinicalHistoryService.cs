namespace MicroEMR.Application.PatientClinicalHistory;

public sealed class PatientClinicalHistoryService(IPatientClinicalHistoryRepository repository) : IPatientClinicalHistoryService
{
    public Task<IReadOnlyList<PatientClinicalHistoryResponse>> ListAsync(Guid patientUid, string status, CancellationToken cancellationToken = default) => repository.ListAsync(patientUid, status is "Archived" or "All" ? status : "Active", cancellationToken);
    public Task<PatientClinicalHistoryResponse> CreateAsync(Guid patientUid, CreatePatientClinicalHistoryRequest request, long actor, CancellationToken cancellationToken = default) { Validate(request); return repository.CreateAsync(patientUid, request, actor, cancellationToken); }
    public Task<PatientClinicalHistoryResponse?> UpdateAsync(Guid patientUid, Guid historyUid, UpdatePatientClinicalHistoryRequest request, long actor, CancellationToken cancellationToken = default) { Validate(request); return repository.UpdateAsync(patientUid, historyUid, request, actor, cancellationToken); }
    public Task<PatientClinicalHistoryResponse?> ArchiveAsync(Guid patientUid, Guid historyUid, string rowVersion, long actor, CancellationToken cancellationToken = default) => repository.ArchiveAsync(patientUid, historyUid, rowVersion, actor, cancellationToken);
    private static void Validate(System.ComponentModel.DataAnnotations.IValidatableObject request)
    {
        var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request, new(request), errors, true))
            throw new PatientClinicalHistoryValidationException(errors[0].ErrorMessage ?? "History information is invalid.");
    }
}
