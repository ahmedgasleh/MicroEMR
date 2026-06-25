
namespace MicroEMR.Api.Models.PatientDocuments;

public interface IPatientDocumentRepository
{
    Task<IReadOnlyList<PatientDocumentListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default);

    Task<PatientDocumentDetailsResponse?> GetByUidAsync(
        Guid documentUid,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentTemplateListItemResponse>>
        GetActiveTemplatesAsync(
            CancellationToken cancellationToken = default);

    Task<DocumentTemplateDetailsResponse?> GetTemplateByUidAsync(
        Guid templateUid,
        CancellationToken cancellationToken = default);

    Task<PatientDocumentDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientDocumentRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default);
}