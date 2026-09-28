namespace MicroEMR.Application.PatientDocuments.Contracts;

public sealed record ConsultationRecipientSuggestion(
    int RecipientOrder, string RecipientType, Guid ProviderUid,
    string DisplayName, string? OrganizationName);

public sealed record ConsultationRecipientState(
    Guid DocumentUid, Guid PatientUid, string RowVersion,
    IReadOnlyList<PatientDocumentRecipientResponse> Recipients,
    IReadOnlyList<ConsultationRecipientSuggestion> Suggestions);
