using MicroEMR.Application.PatientVitals.Contracts;
namespace MicroEMR.Application.PatientVitals.Services;
public interface IPatientVitalService
{
    Task<IReadOnlyList<PatientVitalResponse>> GetByPatientUidAsync(Guid patientUid, CancellationToken cancellationToken = default);
    Task<PatientVitalResponse?> GetByUidAsync(Guid patientUid, Guid patientVitalUid, CancellationToken cancellationToken = default);
    Task<PatientVitalResponse?> CreateAsync(Guid patientUid, CreatePatientVitalRequest request, long? createdBy, CancellationToken cancellationToken = default);
    Task<PatientVitalResponse?> UpdateAsync(Guid patientUid, Guid patientVitalUid, UpdatePatientVitalRequest request, long? updatedBy, CancellationToken cancellationToken = default);
}
