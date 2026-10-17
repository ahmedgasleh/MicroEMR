using MicroEMR.Application.PatientRiskFactors;
namespace MicroEMR.Web.Services.PatientRiskFactors;
public interface IPatientRiskFactorApiClient
{
 Task<IReadOnlyList<PatientRiskFactorResponse>> List(Guid patientUid,string status,CancellationToken token=default);
 Task<PatientRiskFactorResponse?> Get(Guid patientUid,Guid riskFactorUid,CancellationToken token=default);
 Task<PatientRiskFactorResponse> Create(Guid patientUid,UpdatePatientRiskFactorRequest request,CancellationToken token=default);
 Task<PatientRiskFactorResponse?> Update(Guid patientUid,Guid riskFactorUid,UpdatePatientRiskFactorRequest request,CancellationToken token=default);
 Task<PatientRiskFactorResponse?> Archive(Guid patientUid,Guid riskFactorUid,string rowVersion,CancellationToken token=default);
}
