using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Services;
using MicroEMR.Application.Templates.Contracts;
using MicroEMR.Application.Templates.Repositories;
using MicroEMR.Application.Templates.Serialization;

namespace MicroEMR.Application.Templates.Services;

public interface ITemplateAdministrationService
{
    Task<IReadOnlyList<DocumentTemplateDetailsResponse>> ListAsync(string status, TemplateAccessContext context, CancellationToken token=default);
    Task<DocumentTemplateDetailsResponse?> GetAsync(Guid uid, TemplateAccessContext context, CancellationToken token=default);
    Task<TemplateAdministrationResult?> CreateAsync(CreateAdministrativeTemplateRequest request, TemplateAccessContext context, CancellationToken token=default);
    Task<DocumentTemplateDetailsResponse?> UpdateMetadataAsync(Guid uid, UpdateAdministrativeTemplateMetadataRequest request, TemplateAccessContext context, CancellationToken token=default);
    Task<TemplateAdministrationResult?> CloneAsync(Guid uid, CloneDocumentTemplateRequest request, TemplateAccessContext context, CancellationToken token=default);
    Task<DocumentTemplateDetailsResponse?> SetActiveAsync(Guid uid, SetAdministrativeTemplateActiveRequest request, TemplateAccessContext context, CancellationToken token=default);
}

public sealed class TemplateAdministrationService(
    IPatientDocumentService documents, ITemplateAdministrationRepository repository,
    ITemplateDefinitionSerializer serializer, ITemplateAuthorizationService authorization) : ITemplateAdministrationService
{
    public async Task<IReadOnlyList<DocumentTemplateDetailsResponse>> ListAsync(string status, TemplateAccessContext context, CancellationToken token=default) =>
        (await documents.GetTemplatesAsync(status, token)).Where(x=>authorization.CanView(x, context)).ToArray();

    public async Task<DocumentTemplateDetailsResponse?> GetAsync(Guid uid, TemplateAccessContext context, CancellationToken token=default)
    {
        var template=await documents.GetTemplateByUidAsync(uid, token);
        return template is not null && authorization.CanView(template,context) ? template : null;
    }

    public Task<TemplateAdministrationResult?> CreateAsync(CreateAdministrativeTemplateRequest request, TemplateAccessContext context, CancellationToken token=default)
    {
        ValidateMetadata(request.TemplateKind,request.Category);
        if(request.TemplateScope=="Personal" && !request.OwnerUserId.HasValue)request.OwnerUserId=context.UserId;
        authorization.EnsureCanCreate(request.TemplateScope,request.OwnerUserId,context);
        var result=serializer.Process(request.Definition);
        if(!result.IsValid) throw new TemplateDefinitionValidationException(result.Errors);
        return repository.CreateAsync(request,result.DefinitionJson!,context.UserId,token);
    }

    public async Task<DocumentTemplateDetailsResponse?> UpdateMetadataAsync(Guid uid, UpdateAdministrativeTemplateMetadataRequest request, TemplateAccessContext context, CancellationToken token=default)
    {
        var current=await documents.GetTemplateByUidAsync(uid,token);
        if(current is null)return null;
        if(!authorization.CanMutate(current,context))throw new UnauthorizedAccessException("The template cannot be modified by the current user.");
        ValidateMetadata(request.TemplateKind,request.Category);
        if(request.TemplateScope=="Personal" && !request.OwnerUserId.HasValue)request.OwnerUserId=context.UserId;
        authorization.EnsureCanCreate(request.TemplateScope,request.OwnerUserId,context);
        ValidateRowVersion(request.RowVersion);
        return await repository.UpdateMetadataAsync(uid,request,context.UserId,token);
    }

    public async Task<TemplateAdministrationResult?> CloneAsync(Guid uid, CloneDocumentTemplateRequest request, TemplateAccessContext context, CancellationToken token=default)
    {
        var source=await documents.GetTemplateByUidAsync(uid,token);
        if(source is null||!authorization.CanView(source,context))return null;
        authorization.EnsureCanCreate(request.TemplateScope,request.OwnerUserId,context);
        return await repository.CloneAsync(uid,request,context.UserId,token);
    }

    public async Task<DocumentTemplateDetailsResponse?> SetActiveAsync(Guid uid,SetAdministrativeTemplateActiveRequest request,TemplateAccessContext context,CancellationToken token=default)
    {
        var current=await documents.GetTemplateByUidAsync(uid,token);if(current is null)return null;
        if(!authorization.CanMutate(current,context))throw new UnauthorizedAccessException("The template cannot be modified by the current user.");
        ValidateRowVersion(request.RowVersion);return await repository.SetActiveAsync(uid,request,context.UserId,token);
    }

    private static void ValidateMetadata(string kind,string category)
    { if(kind is not ("Document" or "Encounter"))throw new ArgumentException("TemplateKind must be Document or Encounter."); if(string.IsNullOrWhiteSpace(category))throw new ArgumentException("Category is required."); }
    private static void ValidateRowVersion(string value)
    { try{if(Convert.FromBase64String(value).Length!=8)throw new FormatException();}catch(FormatException){throw new ArgumentException("A valid template RowVersion is required.");} }
}
