namespace MicroEMR.Application.PatientReferrals;

public sealed class PatientReferralListItemResponse
{
    public Guid ReferralUid { get; init; }
    public Guid PatientUid { get; init; }
    public required string RecipientName { get; init; }
    public string? RecipientOrganization { get; init; }
    public required string Reason { get; init; }
    public required string Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? SentAtUtc { get; init; }
    public DateTime? FollowUpDueAtUtc { get; init; }
    public bool IsFollowUpOverdue { get; init; }
    public DateTime? ResponseReceivedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public Guid? ReferringProviderUid { get; init; }
    public string? ReferringProviderDisplayName { get; init; }
    public Guid? ArtifactUid { get; init; }
    public Guid? ResponseDocumentUid { get; init; }
    public string? ResponseDocumentTitle { get; init; }
    public required string RowVersion { get; init; }
}

public sealed class PatientReferralDetailsResponse
{
    public Guid ReferralUid { get; init; }
    public Guid PatientUid { get; init; }
    public required string RecipientName { get; init; }
    public string? RecipientOrganization { get; init; }
    public string? RecipientPhone { get; init; }
    public string? RecipientFax { get; init; }
    public required string Reason { get; init; }
    public string? ClinicalSummary { get; init; }
    public required string Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long CreatedBy { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public long? UpdatedBy { get; init; }
    public DateTime? SentAtUtc { get; init; }
    public DateTime? FollowUpDueAtUtc { get; init; }
    public bool IsFollowUpOverdue { get; init; }
    public DateTime? ResponseReceivedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public Guid? ReferringProviderUid { get; init; }
    public string? ReferringProviderDisplayName { get; init; }
    public string? ReferringProviderCredential { get; init; }
    public Guid? ArtifactUid { get; init; }
    public Guid? ResponseDocumentUid { get; init; }
    public string? ResponseDocumentTitle { get; init; }
    public required string RowVersion { get; init; }
}
