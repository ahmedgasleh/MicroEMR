using MicroEMR.Application.ClinicalOutput;

namespace MicroEMR.Application.PatientDocuments.Contracts;

public sealed record SignConsultationRequest(string RowVersion);

// Prepared under the aggregate lock. Identity and time are resolved by SQL, never by the browser.
public sealed record ConsultationSigningContext(PatientDocumentDetailsResponse Document,
    IReadOnlyList<PatientDocumentRecipientResponse> Recipients,
    Guid SignerProviderUid, string SignerDisplayName, DateTime SignedAtUtc);

public interface IConsultationSigningRepository
{
    // Holds the aggregate lock until the artifact metadata and Signed transition commit together.
    Task<PatientDocumentDetailsResponse> SignAsync(Guid patientUid, Guid documentUid, string rowVersion,
        long actorUserId, Func<ConsultationSigningContext, CancellationToken, Task<CreateClinicalOutputArtifact>> createArtifact,
        CancellationToken token = default);
}
