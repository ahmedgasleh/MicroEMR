using MicroEMR.Application.PatientFamilyHistory;
namespace MicroEMR.Web.Services.PatientFamilyHistory;
public interface IPatientFamilyHistoryApiClient
{
 Task<IReadOnlyList<PatientFamilyHistoryResponse>> List(Guid patientUid,string status,CancellationToken token=default);
 Task<PatientFamilyHistoryResponse?> Get(Guid patientUid,Guid familyHistoryUid,CancellationToken token=default);
 Task<PatientFamilyHistoryResponse> Create(Guid patientUid,UpdatePatientFamilyHistoryRequest request,CancellationToken token=default);
 Task<PatientFamilyHistoryResponse?> Update(Guid patientUid,Guid familyHistoryUid,UpdatePatientFamilyHistoryRequest request,CancellationToken token=default);
 Task<PatientFamilyHistoryResponse?> Archive(Guid patientUid,Guid familyHistoryUid,string rowVersion,CancellationToken token=default);
}
