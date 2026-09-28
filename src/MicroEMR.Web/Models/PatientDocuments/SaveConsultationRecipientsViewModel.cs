namespace MicroEMR.Web.Models.PatientDocuments;

public sealed class SaveConsultationRecipientsViewModel
{
    public Guid PatientUid { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<ConsultationRecipientChoice> Recipients { get; set; } = [];
}

public sealed class ConsultationRecipientChoice
{
    public Guid ProviderUid { get; set; }
    public string RecipientType { get; set; } = string.Empty;
}

public sealed record ConsultationRecipientDisplayRow(Guid ProviderUid, string RecipientType,
    string DisplayName, string? OrganizationName, string? Fax, bool IsSuggested);
