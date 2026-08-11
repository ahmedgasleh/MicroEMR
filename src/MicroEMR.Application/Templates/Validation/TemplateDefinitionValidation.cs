using MicroEMR.Application.Templates.Definitions;

namespace MicroEMR.Application.Templates.Validation;

public sealed record TemplateDefinitionValidationError(string Path, string Code, string Message);

public sealed class TemplateDefinitionValidationResult
{
    public TemplateDefinitionValidationResult(IReadOnlyList<TemplateDefinitionValidationError> errors) => Errors = errors;
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<TemplateDefinitionValidationError> Errors { get; }
    public static TemplateDefinitionValidationResult Valid { get; } = new([]);
}

public interface ITemplateDefinitionValidator
{
    TemplateDefinitionValidationResult Validate(TemplateDefinition? definition);
}
