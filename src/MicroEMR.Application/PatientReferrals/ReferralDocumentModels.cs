using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientReferrals;

public sealed class ReferralDocumentLinkResponse
{
    public Guid DocumentUid { get; init; }
    public required string Title { get; init; }
    public required string DocumentType { get; init; }
    public required string DocumentStatus { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedBy { get; init; }
    public string? CreatedByDisplayName { get; init; }
    public DateTime LinkedAtUtc { get; init; }
    public long LinkedBy { get; init; }
    public string? LinkedByDisplayName { get; init; }
}

public sealed class ReferralDocumentMutationRequest
{
    [Required] public required string RowVersion { get; init; }
}

public sealed class ReferralDocumentRuleException(string message) : InvalidOperationException(message);
public sealed class ReferralDocumentConcurrencyException()
    : Exception("The referral changed. Refresh and try again.");
