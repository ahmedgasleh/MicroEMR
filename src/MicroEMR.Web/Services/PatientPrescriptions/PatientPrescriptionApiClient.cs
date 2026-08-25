using System.Net;
using System.Net.Http.Json;
using MicroEMR.Application.PatientPrescriptions;
using MicroEMR.Web.Services;

namespace MicroEMR.Web.Services.PatientPrescriptions;
public interface IPatientPrescriptionApiClient
{
 Task<IReadOnlyList<PatientPrescriptionResponse>> ListAsync(Guid patientUid,CancellationToken token=default);Task<PatientPrescriptionResponse?> GetAsync(Guid patientUid,Guid uid,CancellationToken token=default);Task<PatientPrescriptionResponse> CreateAsync(Guid patientUid,CreatePrescriptionDraftRequest request,CancellationToken token=default);Task<PatientPrescriptionResponse?> UpdateAsync(Guid patientUid,Guid uid,PrescriptionDraftRequest request,CancellationToken token=default);Task<PatientPrescriptionResponse?> ActionAsync(Guid patientUid,Guid uid,string action,PrescriptionTransitionRequest request,CancellationToken token=default);Task<byte[]?> ArtifactAsync(Guid patientUid,Guid uid,CancellationToken token=default);
}
public sealed class PatientPrescriptionApiClient(HttpClient http):IPatientPrescriptionApiClient
{
 public async Task<IReadOnlyList<PatientPrescriptionResponse>> ListAsync(Guid p,CancellationToken t=default)=>await http.GetFromJsonAsync<List<PatientPrescriptionResponse>>($"api/patients/{p}/prescriptions",t)??[];
 public async Task<PatientPrescriptionResponse?> GetAsync(Guid p,Guid u,CancellationToken t=default){var x=await http.GetAsync($"api/patients/{p}/prescriptions/{u}",t);if(x.StatusCode==HttpStatusCode.NotFound)return null;await Ensure(x,t);return await x.Content.ReadFromJsonAsync<PatientPrescriptionResponse>(cancellationToken:t);}
 public async Task<PatientPrescriptionResponse> CreateAsync(Guid p,CreatePrescriptionDraftRequest r,CancellationToken t=default){var x=await http.PostAsJsonAsync($"api/patients/{p}/prescriptions",r,t);await Ensure(x,t);return (await x.Content.ReadFromJsonAsync<PatientPrescriptionResponse>(cancellationToken:t))!;}
 public async Task<PatientPrescriptionResponse?> UpdateAsync(Guid p,Guid u,PrescriptionDraftRequest r,CancellationToken t=default){var x=await http.PutAsJsonAsync($"api/patients/{p}/prescriptions/{u}",r,t);if(x.StatusCode==HttpStatusCode.NotFound)return null;await Ensure(x,t);return await x.Content.ReadFromJsonAsync<PatientPrescriptionResponse>(cancellationToken:t);}
 public async Task<PatientPrescriptionResponse?> ActionAsync(Guid p,Guid u,string a,PrescriptionTransitionRequest r,CancellationToken t=default){var x=await http.PostAsJsonAsync($"api/patients/{p}/prescriptions/{u}/{a}",r,t);if(x.StatusCode==HttpStatusCode.NotFound)return null;await Ensure(x,t);return await x.Content.ReadFromJsonAsync<PatientPrescriptionResponse>(cancellationToken:t);}
 public async Task<byte[]?> ArtifactAsync(Guid p,Guid u,CancellationToken t=default){var x=await http.GetAsync($"api/patients/{p}/prescriptions/{u}/artifact",t);if(x.StatusCode==HttpStatusCode.NotFound)return null;await Ensure(x,t);return await x.Content.ReadAsByteArrayAsync(t);}
 private static async Task Ensure(HttpResponseMessage x,CancellationToken t){if(x.IsSuccessStatusCode)return;var b=await x.Content.ReadAsStringAsync(t);throw new SafeApiResponseException(x.StatusCode,b);}
}
