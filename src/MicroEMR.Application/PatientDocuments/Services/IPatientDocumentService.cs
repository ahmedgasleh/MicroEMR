using MicroEMR.Application.PatientDocuments.Contracts;

namespace MicroEMR.Application.PatientDocuments.Services;

public interface IPatientDocumentService
{
    Task<IReadOnlyList<PatientDocumentListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default);

    Task<PatientDocumentDetailsResponse?> GetByUidAsync(
        Guid documentUid,
        CancellationToken cancellationToken = default);

    Task<PatientDocumentDetailsResponse?> UpdateDraftAsync(
        Guid documentUid,
        UpdatePatientDocumentDraftRequest request,
        long updatedBy,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentTemplateListItemResponse>>
        GetActiveTemplatesAsync(
            CancellationToken cancellationToken = default);

    Task<DocumentTemplateDetailsResponse?> GetTemplateByUidAsync(
        Guid templateUid,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentTemplateDetailsResponse>> GetTemplatesAsync(string statusFilter, CancellationToken cancellationToken = default);
    Task<DocumentTemplateDetailsResponse?> CreateTemplateAsync(CreateDocumentTemplateRequest request, long? createdBy, CancellationToken cancellationToken = default);
    Task<DocumentTemplateDetailsResponse?> UpdateTemplateAsync(Guid templateUid, UpdateDocumentTemplateRequest request, long? updatedBy, CancellationToken cancellationToken = default);
    Task<DocumentTemplateDetailsResponse?> SetTemplateActiveAsync(Guid templateUid, bool isActive, long? updatedBy, CancellationToken cancellationToken = default);

    Task<PatientDocumentDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientDocumentRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default);
}
