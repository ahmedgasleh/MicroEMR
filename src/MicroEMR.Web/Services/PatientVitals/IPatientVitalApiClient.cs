using MicroEMR.Web.Models.PatientVitals;
namespace MicroEMR.Web.Services.PatientVitals;
public interface IPatientVitalApiClient
{
 Task<IReadOnlyList<PatientVitalViewModel>> GetPatientVitalsAsync(Guid patientUid,CancellationToken cancellationToken=default);
 Task<PatientVitalViewModel?> GetPatientVitalAsync(Guid patientUid,Guid vitalUid,CancellationToken cancellationToken=default);
 Task<PatientVitalViewModel?> CreatePatientVitalAsync(Guid patientUid,CreatePatientVitalRequest request,CancellationToken cancellationToken=default);
 Task<PatientVitalViewModel?> UpdatePatientVitalAsync(Guid patientUid,Guid vitalUid,UpdatePatientVitalRequest request,CancellationToken cancellationToken=default);
}
