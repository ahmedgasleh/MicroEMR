using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.PatientReferrals;

public sealed class PatientReferralListItemViewModel
{
    public Guid ReferralUid { get; set; }
    public Guid PatientUid { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientOrganization { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? FollowUpDueAtUtc { get; set; }
    public bool IsFollowUpOverdue { get; set; }
    public DateTime? ResponseReceivedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public Guid? ReferringProviderUid { get; set; }
    public string? ReferringProviderDisplayName { get; set; }
    public Guid? ArtifactUid { get; set; }
    public Guid? ResponseDocumentUid { get; set; }
    public string? ResponseDocumentTitle { get; set; }
}

public sealed class PatientReferralDetailsViewModel
{
    public Guid ReferralUid { get; set; }
    public Guid PatientUid { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientOrganization { get; set; }
    public string? RecipientPhone { get; set; }
    public string? RecipientFax { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ClinicalSummary { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAtUtc { get; set; }
    public long CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public long? UpdatedBy { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? FollowUpDueAtUtc { get; set; }
    public bool IsFollowUpOverdue { get; set; }
    public DateTime? ResponseReceivedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public Guid? ReferringProviderUid { get; set; }
    public string? ReferringProviderDisplayName { get; set; }
    public string? ReferringProviderCredential { get; set; }
    public Guid? ArtifactUid { get; set; }
    public Guid? ResponseDocumentUid { get; set; }
    public string? ResponseDocumentTitle { get; set; }
}

public class CreatePatientReferralViewModel
{
    [Required]
    public Guid PatientUid { get; set; }

    [Required]
    public Guid ReferringProviderUid { get; set; }

    [Required, StringLength(200)]
    public string RecipientName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? RecipientOrganization { get; set; }

    [StringLength(30)]
    public string? RecipientPhone { get; set; }

    [StringLength(30)]
    public string? RecipientFax { get; set; }

    [Required, StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public string? ClinicalSummary { get; set; }
}

public sealed class UpdatePatientReferralDraftViewModel : CreatePatientReferralViewModel
{
    public Guid ReferralUid { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ReferralProviderViewModel
{
    public Guid ProviderUid { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string? Specialty { get; set; }
}

public class ReferralStatusTransitionViewModel
{
    public Guid PatientUid { get; set; }
    public Guid ReferralUid { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ReferralFollowUpViewModel : ReferralStatusTransitionViewModel
{
    public DateTime? FollowUpDueAtUtc { get; set; }
}

public sealed class ReferralResponseDocumentViewModel : ReferralStatusTransitionViewModel
{
    public Guid DocumentUid { get; set; }
}

public sealed class ReferralSupportingDocumentViewModel
{
    public Guid DocumentUid { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentStatus { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LinkedAtUtc { get; set; }
}

public sealed class ReferralDocumentMutationViewModel
{
    public Guid PatientUid { get; set; }
    public Guid ReferralUid { get; set; }
    public Guid DocumentUid { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}
