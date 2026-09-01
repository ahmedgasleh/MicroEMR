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

    Task<PatientReferral?> UpdateDraftAsync(Guid patientUid, Guid referralUid,
        UpdatePatientReferralDraftRequest request, long updatedBy,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    Task<IReadOnlyList<ReferralProviderListItem>> GetActiveProvidersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ReferralProviderListItem>>([]);
    Task<ReferralProvider?> GetProviderAsync(Guid providerUid, CancellationToken cancellationToken = default) => Task.FromResult<ReferralProvider?>(null);
    Task<ReferralArtifactContent?> GetArtifactAsync(Guid patientUid, Guid referralUid,
        CancellationToken cancellationToken = default) => Task.FromResult<ReferralArtifactContent?>(null);

    Task<PatientReferral?> MarkSentAsync(Guid patientUid, Guid referralUid, string rowVersion,
        long updatedBy, CancellationToken cancellationToken = default);

    Task<PatientReferral?> SendWithArtifactAsync(Guid patientUid, Guid referralUid, string rowVersion,
        long updatedBy, ReferralArtifactWrite artifact, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<PatientReferral?> MarkResponseReceivedAsync(Guid patientUid, Guid referralUid, string rowVersion,
        long updatedBy, CancellationToken cancellationToken = default);

    Task<PatientReferral?> CloseAsync(Guid patientUid, Guid referralUid, string rowVersion,
        long updatedBy, CancellationToken cancellationToken = default);
}
