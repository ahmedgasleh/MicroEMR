namespace MicroEMR.Application.PatientReferrals;

public interface IReferralDocumentRepository
{
    Task<IReadOnlyList<ReferralDocumentLinkResponse>> GetByReferralUidAsync(
        Guid patientUid, Guid referralUid, CancellationToken cancellationToken = default);
    Task LinkAsync(Guid patientUid, Guid referralUid, Guid documentUid, string rowVersion,
        long linkedBy, CancellationToken cancellationToken = default);
    Task UnlinkAsync(Guid patientUid, Guid referralUid, Guid documentUid, string rowVersion,
        long unlinkedBy, CancellationToken cancellationToken = default);
}

public interface IReferralDocumentService
{
    Task<IReadOnlyList<ReferralDocumentLinkResponse>?> GetAsync(Guid patientUid, Guid referralUid,
        CancellationToken cancellationToken = default);
    Task LinkAsync(Guid patientUid, Guid referralUid, Guid documentUid,
        ReferralDocumentMutationRequest request, CancellationToken cancellationToken = default);
    Task UnlinkAsync(Guid patientUid, Guid referralUid, Guid documentUid,
        ReferralDocumentMutationRequest request, CancellationToken cancellationToken = default);
}
