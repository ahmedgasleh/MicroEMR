using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientReferrals;

public sealed class ReferralStatusTransitionRequest
{
    [Required]
    public required string RowVersion { get; init; }
}
