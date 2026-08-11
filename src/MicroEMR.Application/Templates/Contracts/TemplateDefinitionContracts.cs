using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Application.Templates.Validation;

namespace MicroEMR.Application.Templates.Contracts;

public sealed class ValidateTemplateDefinitionResponse
{
    public bool IsValid { get; init; }
    public TemplateDefinition? Definition { get; init; }
    public IReadOnlyList<TemplateDefinitionValidationError> Errors { get; init; } = [];
}
