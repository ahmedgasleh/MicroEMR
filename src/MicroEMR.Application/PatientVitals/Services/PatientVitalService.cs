using MicroEMR.Application.PatientVitals.Contracts;
using MicroEMR.Application.PatientVitals.Repositories;
namespace MicroEMR.Application.PatientVitals.Services;
public sealed class PatientVitalService(IPatientVitalRepository repository) : IPatientVitalService
{
    public Task<IReadOnlyList<PatientVitalResponse>> GetByPatientUidAsync(Guid patientUid, CancellationToken cancellationToken = default) => repository.GetByPatientUidAsync(patientUid, cancellationToken);
    public Task<PatientVitalResponse?> GetByUidAsync(Guid patientUid, Guid patientVitalUid, CancellationToken cancellationToken = default) => repository.GetByUidAsync(patientUid, patientVitalUid, cancellationToken);
    public Task<PatientVitalResponse?> CreateAsync(Guid patientUid, CreatePatientVitalRequest request, long? createdBy, CancellationToken cancellationToken = default) => repository.CreateAsync(patientUid, request, createdBy, cancellationToken);
    public Task<PatientVitalResponse?> UpdateAsync(Guid patientUid, Guid patientVitalUid, UpdatePatientVitalRequest request, long? updatedBy, CancellationToken cancellationToken = default) => repository.UpdateAsync(patientUid, patientVitalUid, request, updatedBy, cancellationToken);
}
