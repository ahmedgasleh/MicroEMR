using MicroEMR.Application.PatientCareTeam;
using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.PatientDocuments.Repositories;
using MicroEMR.Application.Providers;
using Microsoft.Extensions.Logging;

namespace MicroEMR.Application.PatientDocuments.Services;

public interface IConsultationRecipientService
{
    Task<ConsultationRecipientState?> GetAsync(Guid patientUid, Guid documentUid,
        bool includeCareTeamSuggestions,
        CancellationToken cancellationToken = default);
    Task<PatientDocumentRecipientsResponse?> ReplaceAsync(Guid patientUid, Guid documentUid,
        ReplacePatientDocumentRecipientsRequest request, long actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed class ConsultationRecipientService(
    IPatientDocumentService documents,
    IPatientDocumentRepository repository,
    IPatientCareTeamService careTeam,
    IProviderAdministrationService providers,
    ILogger<ConsultationRecipientService> logger) : IConsultationRecipientService
{
    private const string ConsultationReportType = "CONSULTATION_REPORT";

    public async Task<ConsultationRecipientState?> GetAsync(Guid patientUid, Guid documentUid,
        bool includeCareTeamSuggestions,
        CancellationToken cancellationToken = default)
    {
        var document = await GetConsultationAsync(patientUid, documentUid, cancellationToken);
        if (document is null) return null;
        var recipients = await documents.GetRecipientsAsync(patientUid, documentUid, cancellationToken) ?? [];
        if (!includeCareTeamSuggestions || document.Status != "Draft" || recipients.Count != 0 ||
            await repository.HasRecipientReplacementAsync(patientUid, documentUid, cancellationToken))
            return new(documentUid, patientUid, document.RowVersion, recipients, []);

        IReadOnlyList<PatientCareTeamRelationship> relationships;
        Dictionary<Guid, ProviderAdministrationItem> activeProviders;
        try
        {
            relationships = await careTeam.ListAsync(patientUid, cancellationToken);
            activeProviders = (await providers.ListAsync("Active", cancellationToken))
                .Where(x => x.IsActive).ToDictionary(x => x.ProviderUid);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Suggestions are optional; failure must not prevent manual recipient selection.
            logger.LogWarning(exception, "Care Team recipient suggestions unavailable for document {DocumentUid}.", documentUid);
            return new(documentUid, patientUid, document.RowVersion, recipients, []);
        }
        var suggestions = new List<ConsultationRecipientSuggestion>();
        Add("REFERRING", "TO");
        Add("PCP", "CC");
        return new(documentUid, patientUid, document.RowVersion, recipients, suggestions);

        void Add(string role, string type)
        {
            var relationship = relationships.FirstOrDefault(x => x.PatientUid == patientUid &&
                x.IsActive && x.IsPrimary && x.EndDate is null && x.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) &&
                x.RelationshipTypeCode == role && activeProviders.ContainsKey(x.ProviderUid) &&
                suggestions.All(s => s.ProviderUid != x.ProviderUid));
            if (relationship is null) return;
            var provider = activeProviders[relationship.ProviderUid];
            suggestions.Add(new(suggestions.Count + 1, type, provider.ProviderUid,
                provider.DisplayName, provider.OrganizationName));
        }
    }

    public async Task<PatientDocumentRecipientsResponse?> ReplaceAsync(Guid patientUid, Guid documentUid,
        ReplacePatientDocumentRecipientsRequest request, long actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (await GetConsultationAsync(patientUid, documentUid, cancellationToken) is null) return null;
        return await documents.ReplaceDraftRecipientsAsync(patientUid, documentUid, request,
            actorUserId, cancellationToken);
    }

    private async Task<PatientDocumentDetailsResponse?> GetConsultationAsync(Guid patientUid,
        Guid documentUid, CancellationToken cancellationToken)
    {
        var document = await documents.GetByUidAsync(documentUid, cancellationToken);
        if (document is null || document.PatientUid != patientUid || !document.TemplateUid.HasValue)
            return null;
        var template = await documents.GetTemplateByUidAsync(document.TemplateUid.Value, cancellationToken);
        return template?.DocumentType == ConsultationReportType ? document : null;
    }
}
