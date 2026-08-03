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

    Task<PatientReferral?> MarkSentAsync(Guid patientUid, Guid referralUid, string rowVersion,
        long updatedBy, CancellationToken cancellationToken = default);

    Task<PatientReferral?> MarkResponseReceivedAsync(Guid patientUid, Guid referralUid, string rowVersion,
        long updatedBy, CancellationToken cancellationToken = default);

    Task<PatientReferral?> CloseAsync(Guid patientUid, Guid referralUid, string rowVersion,
        long updatedBy, CancellationToken cancellationToken = default);
}
