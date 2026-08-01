using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;

namespace MicroEMR.Application.PatientDocuments.Services;

public sealed class DocumentTemplateVersionService(
    IDocumentTemplateVersionRepository repository) : IDocumentTemplateVersionService
{
    public Task<IReadOnlyList<DocumentTemplateVersionResponse>> GetVersionsAsync(Guid templateUid, CancellationToken cancellationToken = default)
    {
        Validate(templateUid, nameof(templateUid));
        return repository.GetByTemplateUidAsync(templateUid, cancellationToken);
    }

    public Task<DocumentTemplateVersionResponse?> CreateDraftVersionAsync(Guid templateUid, long? createdBy, CancellationToken cancellationToken = default)
    {
        Validate(templateUid, nameof(templateUid));
        return repository.CreateDraftAsync(templateUid, createdBy, cancellationToken);
    }

    public Task<DocumentTemplateVersionResponse?> UpdateDraftVersionAsync(Guid templateUid, Guid templateVersionUid, UpdateDocumentTemplateVersionRequest request, long? updatedBy, CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(templateUid, templateVersionUid);
        ValidateRowVersion(request.RowVersion);
        return repository.UpdateDraftAsync(templateUid, templateVersionUid, request, updatedBy, cancellationToken);
    }

    public Task<DocumentTemplateVersionResponse?> PublishVersionAsync(Guid templateUid, Guid templateVersionUid, ChangeDocumentTemplateVersionStatusRequest request, long? publishedBy, CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(templateUid, templateVersionUid);
        ValidateRowVersion(request.RowVersion);
        return repository.PublishAsync(templateUid, templateVersionUid, request.RowVersion, publishedBy, cancellationToken);
    }

    public Task<DocumentTemplateVersionResponse?> RetireVersionAsync(Guid templateUid, Guid templateVersionUid, ChangeDocumentTemplateVersionStatusRequest request, long? retiredBy, CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(templateUid, templateVersionUid);
        ValidateRowVersion(request.RowVersion);
        return repository.RetireAsync(templateUid, templateVersionUid, request.RowVersion, retiredBy, cancellationToken);
    }

    private static void ValidateIdentifiers(Guid templateUid, Guid versionUid)
    {
        Validate(templateUid, nameof(templateUid));
        Validate(versionUid, nameof(versionUid));
    }

    private static void Validate(Guid value, string name)
    {
        if (value == Guid.Empty) throw new ArgumentException("Template identifier is required.", name);
    }

    private static void ValidateRowVersion(string value)
    {
        try
        {
            if (Convert.FromBase64String(value).Length != 8) throw new FormatException();
        }
        catch (FormatException)
        {
            throw new ArgumentException("A valid template version RowVersion is required.", nameof(value));
        }
    }
}
