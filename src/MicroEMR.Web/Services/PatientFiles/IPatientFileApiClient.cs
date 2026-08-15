using MicroEMR.Web.Models.PatientFiles;

namespace MicroEMR.Web.Services.PatientFiles;

public interface IPatientFileApiClient
{
    Task<IReadOnlyList<PatientFileViewModel>> GetByPatientUidAsync(Guid patientUid, CancellationToken cancellationToken = default);
    Task<PatientFileViewModel?> GetByUidAsync(Guid patientUid, Guid fileUid, CancellationToken cancellationToken = default);
    Task<PatientFileViewModel?> UploadAsync(Guid patientUid, IFormFile file, UploadPatientFileViewModel metadata, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> GetContentAsync(Guid patientUid, Guid fileUid, CancellationToken cancellationToken = default);
    Task<PatientFileViewModel?> ArchiveAsync(Guid patientUid,Guid fileUid,string rowVersion,CancellationToken cancellationToken=default);
    Task<PatientFileViewModel?> RestoreAsync(Guid patientUid,Guid fileUid,string rowVersion,CancellationToken cancellationToken=default);
}
