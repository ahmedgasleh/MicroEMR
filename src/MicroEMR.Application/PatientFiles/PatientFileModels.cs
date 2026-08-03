namespace MicroEMR.Application.PatientFiles;

public enum PatientFileStatus { Active, Archived }

public sealed class PatientFile
{
    public Guid FileUid { get; init; }
    public Guid PatientUid { get; init; }
    public required string OriginalFileName { get; init; }
    public required string StorageKey { get; init; }
    public required string ContentType { get; init; }
    public long FileSizeBytes { get; init; }
    public string? FileExtension { get; init; }
    public string? Sha256Hash { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public PatientFileStatus Status { get; init; }
    public DateTime UploadedAtUtc { get; init; }
    public long UploadedBy { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public long? UpdatedBy { get; init; }
    public required string RowVersion { get; init; }
}

public sealed class CreatePatientFileMetadata
{
    public required string OriginalFileName { get; init; }
    public required string StorageKey { get; init; }
    public required string ContentType { get; init; }
    public long FileSizeBytes { get; init; }
    public string? FileExtension { get; init; }
    public string? Sha256Hash { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
}

public static class PatientFileNaming
{
    public static string StorageKey(Guid patientUid, Guid fileUid) => $"patients/{patientUid:N}/{fileUid:N}";
    public static string OriginalFileName(string value)
    {
        var name = Path.GetFileName(value?.Trim());
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A file name is required.", nameof(value));
        return name.Length <= 255 ? name : name[..255];
    }
}
