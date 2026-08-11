using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.PatientDocuments;

public sealed class UpdatePatientDocumentDraftRequest
{
    [Required, StringLength(250)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    public string? Content { get; set; }
    public string? StructuredDataJson { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [Required]
    public string ContentRowVersion { get; set; } = string.Empty;
}
