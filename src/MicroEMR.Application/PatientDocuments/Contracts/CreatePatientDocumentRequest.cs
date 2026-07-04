using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Application.PatientDocuments.Contracts;

public sealed class CreatePatientDocumentRequest
{
    public Guid? TemplateUid { get; set; }

    [Required]
    [StringLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [StringLength(250)]
    public string Title { get; set; } = string.Empty;

    public string? Content { get; set; }
}