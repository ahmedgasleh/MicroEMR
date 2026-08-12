namespace MicroEMR.Application.ClinicalOutput;

public static class ClinicalArtifactTypes
{
    public const string Encounter = "Encounter";
    public const string FinalPdf = "FinalPdf";
    public const string FileSystem = "FileSystem";
}

public sealed record ClinicalOutputArtifact(
    Guid ArtifactUid, Guid PatientUid, string SourceType, Guid SourceUid,
    Guid TemplateVersionUid, string ArtifactType, string StorageProvider,
    string StorageKey, string MimeType, long FileSizeBytes, string Sha256,
    string Status, long? CreatedBy, DateTime CreatedAtUtc);

public sealed record CreateClinicalOutputArtifact(
    Guid ArtifactUid, Guid PatientUid, string SourceType, Guid SourceUid,
    Guid TemplateVersionUid, string ArtifactType, string StorageProvider,
    string StorageKey, string MimeType, long FileSizeBytes, string Sha256,
    long? CreatedBy);

public interface IClinicalOutputArtifactRepository
{
    Task<ClinicalOutputArtifact?> GetFinalBySourceAsync(string sourceType, Guid sourceUid, CancellationToken token = default);
    Task<ClinicalOutputArtifact> CreateAsync(CreateClinicalOutputArtifact artifact, CancellationToken token = default);
    Task RecordFailureAsync(Guid patientUid, string sourceType, Guid sourceUid, Guid templateVersionUid,
        long? actorUserId, string failureCode, CancellationToken token = default);
}

public sealed record ClinicalArtifactContent(Stream Content, string FileName, string MimeType, long FileSizeBytes);

public interface IClinicalArtifactService
{
    Task<ClinicalOutputArtifact?> EnsureEncounterFinalPdfAsync(Guid encounterUid, long? actorUserId, CancellationToken token = default);
    Task<ClinicalArtifactContent?> OpenEncounterFinalPdfAsync(Guid encounterUid, CancellationToken token = default);
}
