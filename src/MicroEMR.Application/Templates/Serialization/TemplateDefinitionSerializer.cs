using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Application.Templates.Validation;

namespace MicroEMR.Application.Templates.Serialization;

public sealed record TemplateDefinitionProcessingResult(
    bool IsValid, TemplateDefinition? Definition, string? DefinitionJson,
    IReadOnlyList<TemplateDefinitionValidationError> Errors);

public interface ITemplateDefinitionSerializer
{
    TemplateDefinitionProcessingResult Process(string? definitionJson);
    TemplateDefinitionProcessingResult Process(TemplateDefinition? definition);
}

public sealed class TemplateDefinitionSerializer(ITemplateDefinitionValidator validator) : ITemplateDefinitionSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = false
    };

    public TemplateDefinitionProcessingResult Process(string? definitionJson)
    {
        if (string.IsNullOrWhiteSpace(definitionJson)) return Invalid("$", "DefinitionRequired", "A template definition is required.");
        if (Encoding.UTF8.GetByteCount(definitionJson) > TemplateDefinitionLimits.MaximumJsonBytes)
            return Invalid("$", "DefinitionTooLarge", $"Definition JSON may not exceed {TemplateDefinitionLimits.MaximumJsonBytes} bytes.");
        try { return Process(JsonSerializer.Deserialize<TemplateDefinition>(definitionJson, Options)); }
        catch (JsonException exception) { return Invalid(exception.Path ?? "$", "MalformedJson", "Definition JSON is malformed or contains an unknown property."); }
    }

    public TemplateDefinitionProcessingResult Process(TemplateDefinition? definition)
    {
        var validation = validator.Validate(definition);
        if (!validation.IsValid) return new(false, definition, null, validation.Errors);
        var normalized = Normalize(definition!);
        var json = JsonSerializer.Serialize(normalized, Options);
        if (Encoding.UTF8.GetByteCount(json) > TemplateDefinitionLimits.MaximumJsonBytes)
            return Invalid("$", "DefinitionTooLarge", $"Definition JSON may not exceed {TemplateDefinitionLimits.MaximumJsonBytes} bytes.");
        return new(true, normalized, json, []);
    }

    private static TemplateDefinition Normalize(TemplateDefinition source) => new()
    {
        SchemaVersion = source.SchemaVersion,
        Sections = source.Sections!.OrderBy(x => x.Order).ThenBy(x => x.Id, StringComparer.Ordinal).Select(section => new TemplateSectionDefinition
        {
            Id=section.Id, Key=section.Key, Title=section.Title, Order=section.Order,
            Fields=section.Fields!.OrderBy(x=>x.Order).ThenBy(x=>x.Id,StringComparer.Ordinal).Select(field=>new TemplateFieldDefinition
            {
                Id=field.Id,Key=field.Key,Type=field.Type,Label=field.Label,Order=field.Order,Required=field.Required,
                DefaultValue=field.DefaultValue,HelpText=field.HelpText,Placeholder=field.Placeholder,Content=field.Content,
                Options=field.Options?.OrderBy(x=>x.Order).ThenBy(x=>x.Value,StringComparer.Ordinal).Select(x=>new TemplateFieldOption{Value=x.Value,Label=x.Label,Order=x.Order}).ToList()
            }).ToList()
        }).ToList()
    };

    private static TemplateDefinitionProcessingResult Invalid(string path, string code, string message) => new(false, null, null, [new(path, code, message)]);
}
