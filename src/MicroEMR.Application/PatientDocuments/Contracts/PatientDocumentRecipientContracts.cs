namespace MicroEMR.Application.PatientDocuments.Contracts;

public sealed record PatientDocumentRecipientSelection(int RecipientOrder, string RecipientType, Guid ProviderUid);

public sealed class ReplacePatientDocumentRecipientsRequest
{
    public string RowVersion { get; set; } = string.Empty;
    public IReadOnlyList<PatientDocumentRecipientSelection> Recipients { get; set; } = [];
}

public sealed record PatientDocumentRecipientResponse(
    Guid RecipientUid, int RecipientOrder, string RecipientType, Guid ProviderUid,
    string DisplayNameSnapshot, string? OrganizationNameSnapshot, string? FaxSnapshot);

public sealed record PatientDocumentRecipientsResponse(
    Guid DocumentUid, string RowVersion, IReadOnlyList<PatientDocumentRecipientResponse> Recipients);
