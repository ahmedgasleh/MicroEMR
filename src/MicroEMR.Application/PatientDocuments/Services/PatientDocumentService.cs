using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;

namespace MicroEMR.Application.PatientDocuments.Services;

public sealed class PatientDocumentService : IPatientDocumentService
{
    private readonly IPatientDocumentRepository _repository;

    public PatientDocumentService(
        IPatientDocumentRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<PatientDocumentListItemResponse>>
        GetByPatientUidAsync(
            Guid patientUid,
            CancellationToken cancellationToken = default)
    {
        return _repository.GetByPatientUidAsync(
            patientUid,
            cancellationToken);
    }

    public Task<PatientDocumentDetailsResponse?> GetByUidAsync(
        Guid documentUid,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByUidAsync(
            documentUid,
            cancellationToken);
    }

    public Task<PatientDocumentDetailsResponse?> UpdateDraftAsync(
        Guid documentUid,
        UpdatePatientDocumentDraftRequest request,
        long updatedBy,
        CancellationToken cancellationToken = default)
    {
        return _repository.UpdateDraftAsync(
            documentUid,
            request,
            updatedBy,
            cancellationToken);
    }

    public Task<IReadOnlyList<DocumentTemplateListItemResponse>>
        GetActiveTemplatesAsync(
            CancellationToken cancellationToken = default)
    {
        return _repository.GetActiveTemplatesAsync(cancellationToken);
    }

    public Task<DocumentTemplateDetailsResponse?> GetTemplateByUidAsync(
        Guid templateUid,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetTemplateByUidAsync(
            templateUid,
            cancellationToken);
    }

    public Task<IReadOnlyList<DocumentTemplateDetailsResponse>> GetTemplatesAsync(string statusFilter, CancellationToken cancellationToken = default) =>
        _repository.GetTemplatesAsync(NormalizeStatus(statusFilter), cancellationToken);

    public Task<DocumentTemplateDetailsResponse?> CreateTemplateAsync(CreateDocumentTemplateRequest request, long? createdBy, CancellationToken cancellationToken = default) =>
        _repository.CreateTemplateAsync(request, createdBy, cancellationToken);

    public Task<DocumentTemplateDetailsResponse?> UpdateTemplateAsync(Guid templateUid, UpdateDocumentTemplateRequest request, long? updatedBy, CancellationToken cancellationToken = default) =>
        _repository.UpdateTemplateAsync(templateUid, request, updatedBy, cancellationToken);

    public Task<DocumentTemplateDetailsResponse?> SetTemplateActiveAsync(Guid templateUid, bool isActive, long? updatedBy, CancellationToken cancellationToken = default) =>
        _repository.SetTemplateActiveAsync(templateUid, isActive, updatedBy, cancellationToken);

    private static string NormalizeStatus(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "inactive" => "Inactive",
        "all" => "All",
        _ => "Active"
    };

    public Task<PatientDocumentDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientDocumentRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default)
    {
        return _repository.CreateAsync(
            patientUid,
            request,
            createdBy,
            cancellationToken);
    }
}
