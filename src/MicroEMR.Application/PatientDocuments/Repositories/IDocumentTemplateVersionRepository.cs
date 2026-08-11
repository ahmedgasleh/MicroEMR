using MicroEMR.Application.PatientDocuments.Contracts;

namespace MicroEMR.Application.PatientDocuments.Repositories;

public interface IDocumentTemplateVersionRepository
{
    Task<DocumentTemplateVersionResponse?> GetByUidAsync(Guid templateVersionUid, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentTemplateVersionResponse>> GetByTemplateUidAsync(
        Guid templateUid,
        CancellationToken cancellationToken = default);

    Task<DocumentTemplateVersionResponse?> CreateDraftAsync(
        Guid templateUid,
        long? createdBy,
        CancellationToken cancellationToken = default);

    Task<DocumentTemplateVersionResponse?> UpdateDraftAsync(
        Guid templateUid,
        Guid templateVersionUid,
        UpdateDocumentTemplateVersionRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default);

    Task<DocumentTemplateVersionResponse?> PublishAsync(
        Guid templateUid,
        Guid templateVersionUid,
        string rowVersion,
        long? publishedBy,
        CancellationToken cancellationToken = default);

    Task<DocumentTemplateVersionResponse?> RetireAsync(
        Guid templateUid,
        Guid templateVersionUid,
        string rowVersion,
        long? retiredBy,
        CancellationToken cancellationToken = default);
}
