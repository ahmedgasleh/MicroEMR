namespace MicroEMR.Application.PatientReferrals;

public sealed record ReferralReportSnapshot(string SourceType, Guid SourceUid, string Title, string DocumentType,
    string Status, string RowVersion, string MimeType, long ContentLength, string Sha256, int? AppendixNumber);

public sealed record ReferralReportAppendix(int Number, string HeadingHtml, byte[] PdfContent);
public sealed record ReferralReportComposition(string Html, IReadOnlyList<ReferralReportAppendix> Appendices,
    IReadOnlyList<ReferralReportSnapshot> Sources);

public interface IReferralReportContentService
{
    Task<IReadOnlyList<ReferralClinicalOption>> GetFileOptionsAsync(Guid patientUid, CancellationToken token = default);
    Task<ReferralReportComposition> ComposeAsync(Guid patientUid, IReadOnlyList<ReferralDocumentLinkResponse> documents,
        IReadOnlyList<PatientReferralClinicalSelectionResponse> selections, CancellationToken token = default);
}

// PDF import is an Infrastructure concern; report/source selection remains in Application.
public interface IReferralPdfAssembler
{
    Task<byte[]> CombineAsync(IReadOnlyList<byte[]> parts, CancellationToken token = default);
}
