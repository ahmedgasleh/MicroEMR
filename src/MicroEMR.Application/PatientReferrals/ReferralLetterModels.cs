namespace MicroEMR.Application.PatientReferrals;

public sealed record ReferralProvider(
    Guid ProviderUid, string DisplayName, string ProviderType, string? BillingNumber, string? Specialty);

public sealed record ReferralProviderListItem(
    Guid ProviderUid, string DisplayName, string ProviderType, string? Specialty);

public sealed record ReferralArtifactWrite(
    Guid ArtifactUid, DateTime SentAtUtc, byte[] PdfContent, string FileName, string Sha256, string SnapshotJson,
    string ProviderDisplayName, string? ProviderCredential);

public sealed record ReferralArtifactContent(
    Guid ArtifactUid, string MimeType, string FileName, byte[] PdfContent, long FileSizeBytes,
    string Sha256, string SnapshotJson, DateTime CreatedAtUtc);

public sealed record ReferralArtifactDownload(Stream Content, string FileName, string MimeType, long Length);
