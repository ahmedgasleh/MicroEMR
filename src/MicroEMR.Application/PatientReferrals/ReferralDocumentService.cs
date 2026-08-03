using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Application.PatientDocuments.Repositories;

namespace MicroEMR.Application.PatientReferrals;

public sealed class ReferralDocumentService(
    IPatientReferralRepository referrals,
    IPatientDocumentRepository documents,
    IReferralDocumentRepository links,
    IAuthenticatedClinicalUserAccessor actor) : IReferralDocumentService
{
    public async Task<IReadOnlyList<ReferralDocumentLinkResponse>?> GetAsync(
        Guid patientUid, Guid referralUid, CancellationToken cancellationToken = default) =>
        await referrals.GetByUidAsync(patientUid, referralUid, cancellationToken) is null
            ? null : await links.GetByReferralUidAsync(patientUid, referralUid, cancellationToken);

    public Task LinkAsync(Guid patientUid, Guid referralUid, Guid documentUid,
        ReferralDocumentMutationRequest request, CancellationToken cancellationToken = default) =>
        MutateAsync(patientUid, referralUid, documentUid, request, links.LinkAsync, cancellationToken);

    public Task UnlinkAsync(Guid patientUid, Guid referralUid, Guid documentUid,
        ReferralDocumentMutationRequest request, CancellationToken cancellationToken = default) =>
        MutateAsync(patientUid, referralUid, documentUid, request, links.UnlinkAsync, cancellationToken);

    private async Task MutateAsync(Guid patientUid, Guid referralUid, Guid documentUid,
        ReferralDocumentMutationRequest request,
        Func<Guid, Guid, Guid, string, long, CancellationToken, Task> mutate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var referral = await referrals.GetByUidAsync(patientUid, referralUid, cancellationToken)
            ?? throw new KeyNotFoundException("Referral not found.");
        if (referral.Status != ReferralStatus.Draft)
            throw new ReferralDocumentRuleException("Supporting documents can only be changed while the referral is Draft.");
        if (!string.Equals(referral.RowVersion, request.RowVersion, StringComparison.Ordinal))
            throw new ReferralDocumentConcurrencyException();
        var document = await documents.GetByUidAsync(documentUid, cancellationToken)
            ?? throw new KeyNotFoundException("Document not found.");
        if (document.PatientUid != patientUid)
            throw new KeyNotFoundException("Document not found.");
        var userId = await actor.GetRequiredUserIdAsync(cancellationToken);
        await mutate(patientUid, referralUid, documentUid, request.RowVersion, userId, cancellationToken);
    }
}
