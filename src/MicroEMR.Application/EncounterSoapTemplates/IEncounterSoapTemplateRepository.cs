namespace MicroEMR.Application.EncounterSoapTemplates;

public interface IEncounterSoapTemplateRepository
{
    Task<IReadOnlyList<EncounterSoapTemplateResponse>> GetAllAsync(string statusFilter, CancellationToken cancellationToken = default);
    Task<EncounterSoapTemplateResponse?> GetByUidAsync(Guid uid, CancellationToken cancellationToken = default);
    Task<EncounterSoapTemplateResponse?> CreateAsync(CreateEncounterSoapTemplateRequest request, long? userId, CancellationToken cancellationToken = default);
    Task<EncounterSoapTemplateResponse?> UpdateAsync(Guid uid, UpdateEncounterSoapTemplateRequest request, long? userId, CancellationToken cancellationToken = default);
    Task<EncounterSoapTemplateResponse?> SetActiveAsync(Guid uid, bool isActive, long? userId, CancellationToken cancellationToken = default);
}
