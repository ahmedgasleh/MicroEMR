using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientReferrals;

public sealed class CreatePatientReferralRequest
{
    [Required, StringLength(200)]
    public required string RecipientName { get; init; }

    [StringLength(200)]
    public string? RecipientOrganization { get; init; }

    [StringLength(30)]
    public string? RecipientPhone { get; init; }

    [StringLength(30)]
    public string? RecipientFax { get; init; }

    [Required, StringLength(1000)]
    public required string Reason { get; init; }

    public string? ClinicalSummary { get; init; }
}
