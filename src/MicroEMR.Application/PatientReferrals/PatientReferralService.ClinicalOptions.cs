namespace MicroEMR.Application.PatientReferrals;

public sealed partial class PatientReferralService
{
    public async Task<ReferralClinicalOptionsResponse> GetClinicalSelectionOptionsAsync(Guid patientUid,
        CancellationToken cancellationToken = default)
    {
        await RequireSelectionAccessAsync(MicroEMR.Application.AccessProfiles.PermissionKeys.ReferralsManage,cancellationToken);
        await EnsurePatientExistsAsync(patientUid,cancellationToken);
        return await (clinicalContent ?? throw new InvalidOperationException("Referral clinical content service is unavailable."))
            .GetOptionsAsync(patientUid,cancellationToken);
    }
}
