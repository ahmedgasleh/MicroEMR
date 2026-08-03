namespace MicroEMR.Application.PatientReferrals;

public interface IPatientReferralRepository
{
    Task<IReadOnlyList<PatientReferral>> GetByPatientUidAsync(
        Guid patientUid,
        CancellationToken cancellationToken = default);

    Task<PatientReferral?> GetByUidAsync(
        Guid patientUid,
        Guid referralUid,
        CancellationToken cancellationToken = default);

    Task<PatientReferral> CreateAsync(
        Guid patientUid,
        CreatePatientReferralRequest request,
        long createdBy,
        CancellationToken cancellationToken = default);
}
