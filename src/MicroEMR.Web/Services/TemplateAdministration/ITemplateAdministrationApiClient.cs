using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Web.Models.TemplateAdministration;

namespace MicroEMR.Web.Services.TemplateAdministration;

public interface ITemplateAdministrationApiClient
{
    Task<IReadOnlyList<TemplateDetailsModel>> ListAsync(string status,CancellationToken token=default);
    Task<TemplateDetailsModel?> GetAsync(Guid uid,CancellationToken token=default);
    Task<IReadOnlyList<TemplateVersionModel>> GetVersionsAsync(Guid uid,CancellationToken token=default);
    Task<TemplateAdministrationResultModel?> CreateAsync(CreateTemplateViewModel model,CancellationToken token=default);
    Task<TemplateVersionModel?> OpenDraftAsync(Guid uid,CancellationToken token=default);
    Task<TemplateVersionModel?> SaveDraftAsync(SaveTemplateDefinitionViewModel model,CancellationToken token=default);
    Task<TemplateValidationResponseModel> ValidateAsync(TemplateDefinition definition,CancellationToken token=default);
    Task<TemplateVersionModel?> PublishAsync(PublishTemplateViewModel model,CancellationToken token=default);
    Task<TemplateAdministrationResultModel?> CloneAsync(CloneTemplateViewModel model,CancellationToken token=default);
    Task<TemplateDetailsModel?> SetActiveAsync(SetTemplateActiveViewModel model,CancellationToken token=default);
    Task<TemplateDetailsModel?> UpdateMetadataAsync(UpdateTemplateMetadataViewModel model,CancellationToken token=default);
}

public sealed class TemplateAdministrationApiException(int statusCode,string responseBody)
    : Exception("The template operation could not be completed.")
{
    public int StatusCode { get; }=statusCode;
    public string ResponseBody { get; }=responseBody;
}
