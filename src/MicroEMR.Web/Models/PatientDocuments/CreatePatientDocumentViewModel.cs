using System.ComponentModel.DataAnnotations;

namespace MicroEMR.Web.Models.PatientDocuments;

public sealed class CreatePatientDocumentViewModel
    : IValidatableObject
{
    public Guid PatientUid { get; set; }

    public Guid? TemplateUid { get; set; }

    [Required]
    [StringLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [StringLength(250)]
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public IReadOnlyList<DocumentTemplateListItemResponse> Templates
        { get; set; } =
        Array.Empty<DocumentTemplateListItemResponse>();

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (PatientUid == Guid.Empty)
        {
            yield return new ValidationResult(
                "A patient is required.",
                new[] { nameof(PatientUid) });
        }
    }
}
