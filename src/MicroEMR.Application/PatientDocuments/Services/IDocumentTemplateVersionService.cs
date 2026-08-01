using MicroEMR.Application.PatientDocuments.Contracts;

namespace MicroEMR.Application.PatientDocuments.Services;

public interface IDocumentTemplateVersionService
{
    Task<IReadOnlyList<DocumentTemplateVersionResponse>> GetVersionsAsync(Guid templateUid, CancellationToken cancellationToken = default);
    Task<DocumentTemplateVersionResponse?> CreateDraftVersionAsync(Guid templateUid, long? createdBy, CancellationToken cancellationToken = default);
    Task<DocumentTemplateVersionResponse?> UpdateDraftVersionAsync(Guid templateUid, Guid templateVersionUid, UpdateDocumentTemplateVersionRequest request, long? updatedBy, CancellationToken cancellationToken = default);
    Task<DocumentTemplateVersionResponse?> PublishVersionAsync(Guid templateUid, Guid templateVersionUid, ChangeDocumentTemplateVersionStatusRequest request, long? publishedBy, CancellationToken cancellationToken = default);
    Task<DocumentTemplateVersionResponse?> RetireVersionAsync(Guid templateUid, Guid templateVersionUid, ChangeDocumentTemplateVersionStatusRequest request, long? retiredBy, CancellationToken cancellationToken = default);
}
