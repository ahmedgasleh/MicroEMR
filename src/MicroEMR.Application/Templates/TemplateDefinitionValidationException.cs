using MicroEMR.Application.Templates.Validation;

namespace MicroEMR.Application.Templates;

public sealed class TemplateDefinitionValidationException(IReadOnlyList<TemplateDefinitionValidationError> errors)
    : Exception("The template definition is invalid.")
{
    public IReadOnlyList<TemplateDefinitionValidationError> Errors { get; } = errors;
}
