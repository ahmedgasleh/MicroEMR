using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.Templates.Contracts;

namespace MicroEMR.Application.Templates.Repositories;

public interface ITemplateAdministrationRepository
{
    Task<TemplateAdministrationResult?> CreateAsync(CreateAdministrativeTemplateRequest request, string definitionJson, long actorUserId, CancellationToken cancellationToken=default);
    Task<DocumentTemplateDetailsResponse?> UpdateMetadataAsync(Guid templateUid, UpdateAdministrativeTemplateMetadataRequest request, long actorUserId, CancellationToken cancellationToken=default);
    Task<TemplateAdministrationResult?> CloneAsync(Guid sourceTemplateUid, CloneDocumentTemplateRequest request, long actorUserId, CancellationToken cancellationToken=default);
    Task<DocumentTemplateDetailsResponse?> SetActiveAsync(Guid templateUid, SetAdministrativeTemplateActiveRequest request, long actorUserId, CancellationToken cancellationToken=default);
}
