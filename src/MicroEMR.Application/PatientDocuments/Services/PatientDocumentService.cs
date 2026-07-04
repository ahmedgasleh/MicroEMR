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
