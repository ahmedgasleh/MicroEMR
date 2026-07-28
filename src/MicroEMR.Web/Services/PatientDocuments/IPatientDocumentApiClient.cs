using MicroEMR.Web.Models.PatientDocuments;

namespace MicroEMR.Web.Services.PatientDocuments;

public interface IPatientDocumentApiClient
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

    Task<IReadOnlyList<DocumentTemplateDetailsResponse>> GetDocumentTemplatesAsync(string statusFilter, CancellationToken cancellationToken = default);
    Task<DocumentTemplateDetailsResponse?> CreateDocumentTemplateAsync(SaveDocumentTemplateRequest request, CancellationToken cancellationToken = default);
    Task<DocumentTemplateDetailsResponse?> UpdateDocumentTemplateAsync(Guid templateUid, SaveDocumentTemplateRequest request, CancellationToken cancellationToken = default);
    Task<DocumentTemplateDetailsResponse?> SetDocumentTemplateActiveAsync(Guid templateUid, bool isActive, CancellationToken cancellationToken = default);

    Task<PatientDocumentDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientDocumentRequest request,
        CancellationToken cancellationToken = default);
}
