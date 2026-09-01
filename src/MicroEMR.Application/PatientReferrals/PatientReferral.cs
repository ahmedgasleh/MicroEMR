namespace MicroEMR.Application.PatientReferrals;

public sealed class PatientReferral
{
    public Guid ReferralUid { get; init; }
    public Guid PatientUid { get; init; }
    public required string RecipientName { get; init; }
    public string? RecipientOrganization { get; init; }
    public string? RecipientPhone { get; init; }
    public string? RecipientFax { get; init; }
    public required string Reason { get; init; }
    public string? ClinicalSummary { get; init; }
    public Guid? ReferringProviderUid { get; init; }
    public string? ReferringProviderDisplayNameSnapshot { get; init; }
    public string? ReferringProviderCredentialSnapshot { get; init; }
    public Guid? ArtifactUid { get; init; }
    public ReferralStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public long CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public long? UpdatedBy { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime? FollowUpDueAt { get; init; }
    public DateTime? ResponseReceivedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
    public Guid? ResponseDocumentUid { get; init; }
    public string? ResponseDocumentTitle { get; init; }
    public required string RowVersion { get; init; }
}
