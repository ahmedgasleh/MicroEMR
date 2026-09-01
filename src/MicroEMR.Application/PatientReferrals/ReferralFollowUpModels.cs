using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientReferrals;

public sealed class SetReferralFollowUpRequest
{
    [Required] public required string RowVersion { get; init; }
    public DateTime? FollowUpDueAtUtc { get; init; }
}

public sealed class ReferralResponseDocumentRequest
{
    [Required] public required string RowVersion { get; init; }
    public Guid DocumentUid { get; init; }
}

public static class ReferralFollowUpRule
{
    public static bool IsOverdue(DateTime? dueAtUtc, ReferralStatus status, DateTime utcNow) =>
        dueAtUtc.HasValue && dueAtUtc.Value < utcNow && status == ReferralStatus.Sent;
}
