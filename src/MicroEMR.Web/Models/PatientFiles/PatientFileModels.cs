using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.PatientFiles;

public sealed class PatientFileViewModel
{
    public Guid FileUid { get; set; }
    public Guid PatientUid { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? FileExtension { get; set; }
    public string? Sha256Hash { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime UploadedAtUtc { get; set; }
    public long UploadedBy { get; set; }
    public string? UploadedByDisplayName { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedByDisplayName { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class UploadPatientFileViewModel
{
    [Required] public IFormFile? File { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    [StringLength(100)] public string? Category { get; set; }
}
