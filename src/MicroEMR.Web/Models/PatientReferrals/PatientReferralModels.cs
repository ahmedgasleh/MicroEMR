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
    public DateTime? ResponseReceivedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string RowVersion { get; set; } = string.Empty;
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
    public DateTime? ResponseReceivedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CreatePatientReferralViewModel
{
    [Required]
    public Guid PatientUid { get; set; }

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

public sealed class ReferralStatusTransitionViewModel
{
    public Guid PatientUid { get; set; }
    public Guid ReferralUid { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
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
