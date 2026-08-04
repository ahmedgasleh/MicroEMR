using MicroEMR.Web.Models.PatientFiles;

namespace MicroEMR.Web.Services.PatientFiles;

public interface IPatientFileApiClient
{
    Task<IReadOnlyList<PatientFileViewModel>> GetByPatientUidAsync(Guid patientUid, CancellationToken cancellationToken = default);
    Task<PatientFileViewModel?> GetByUidAsync(Guid patientUid, Guid fileUid, CancellationToken cancellationToken = default);
    Task<PatientFileViewModel?> UploadAsync(Guid patientUid, IFormFile file, string? description, string? category, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> GetContentAsync(Guid patientUid, Guid fileUid, CancellationToken cancellationToken = default);
}
