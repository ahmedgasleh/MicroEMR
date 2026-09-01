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

    Task<PatientReferralDetailsResponse?> UpdateDraftAsync(Guid patientUid, Guid referralUid,
        UpdatePatientReferralDraftRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<IReadOnlyList<ReferralProviderListItem>> GetActiveProvidersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ReferralProviderListItem>>([]);
    Task<byte[]?> PreviewLetterAsync(Guid patientUid, Guid referralUid, CancellationToken cancellationToken = default) => Task.FromResult<byte[]?>(null);
    Task<ReferralArtifactDownload?> OpenArtifactAsync(Guid patientUid, Guid referralUid,
        CancellationToken cancellationToken = default) => Task.FromResult<ReferralArtifactDownload?>(null);

    Task<PatientReferralDetailsResponse?> MarkSentAsync(Guid patientUid, Guid referralUid,
        ReferralStatusTransitionRequest request, CancellationToken cancellationToken = default);

    Task<PatientReferralDetailsResponse?> MarkResponseReceivedAsync(Guid patientUid, Guid referralUid,
        ReferralStatusTransitionRequest request, CancellationToken cancellationToken = default);

    Task<PatientReferralDetailsResponse?> SetFollowUpDueAsync(Guid patientUid, Guid referralUid,
        SetReferralFollowUpRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    Task<PatientReferralDetailsResponse?> SetResponseDocumentAsync(Guid patientUid, Guid referralUid,
        ReferralResponseDocumentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    Task<PatientReferralDetailsResponse?> ClearResponseDocumentAsync(Guid patientUid, Guid referralUid,
        ReferralStatusTransitionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    Task<PatientReferralDetailsResponse?> CloseAsync(Guid patientUid, Guid referralUid,
        ReferralStatusTransitionRequest request, CancellationToken cancellationToken = default);
}
