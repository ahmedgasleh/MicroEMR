namespace MicroEMR.Application.PatientReferrals;

public interface IPatientReferralService
{
    Task<IReadOnlyList<PatientReferralListItemResponse>> GetByPatientUidAsync(
        Guid patientUid,
        CancellationToken cancellationToken = default);

    Task<PatientReferralDetailsResponse?> GetByUidAsync(
        Guid patientUid,
        Guid referralUid,
        CancellationToken cancellationToken = default);

    Task<PatientReferralDetailsResponse> CreateAsync(
        Guid patientUid,
        CreatePatientReferralRequest request,
        CancellationToken cancellationToken = default);

    Task<PatientReferralDetailsResponse?> MarkSentAsync(Guid patientUid, Guid referralUid,
        ReferralStatusTransitionRequest request, CancellationToken cancellationToken = default);

    Task<PatientReferralDetailsResponse?> MarkResponseReceivedAsync(Guid patientUid, Guid referralUid,
        ReferralStatusTransitionRequest request, CancellationToken cancellationToken = default);

    Task<PatientReferralDetailsResponse?> CloseAsync(Guid patientUid, Guid referralUid,
        ReferralStatusTransitionRequest request, CancellationToken cancellationToken = default);
}
