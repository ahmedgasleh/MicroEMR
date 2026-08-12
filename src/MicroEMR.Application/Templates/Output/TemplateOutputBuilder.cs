using System.Globalization;
using System.Text.Json;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Application.Templates.Runtime;
using MicroEMR.Application.Templates.Variables;

namespace MicroEMR.Application.Templates.Output;

public interface ITemplateOutputBuilder
{
    TemplateOutputDocument Build(TemplateDefinition definition, TemplateInstanceData data,
        TemplateVariableContext? context = null);
}

public sealed class TemplateOutputBuilder(ITemplateVariableResolver variables) : ITemplateOutputBuilder
{
    public TemplateOutputDocument Build(TemplateDefinition definition, TemplateInstanceData data,
        TemplateVariableContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(data);

        var sections = definition.Sections!
            .OrderBy(section => section.Order)
            .Select(section => new TemplateOutputSection(
                section.Key,
                Resolve(section.Title ?? string.Empty, context),
                section.Order,
                section.Fields!.OrderBy(field => field.Order)
                    .Select(field => BuildItem(field, data, context))
                    .Where(item => item is not null)
                    .Cast<TemplateOutputItem>()
                    .ToArray()))
            .ToArray();
        return new TemplateOutputDocument(sections);
    }

    private TemplateOutputItem? BuildItem(TemplateFieldDefinition field, TemplateInstanceData data,
        TemplateVariableContext? context)
    {
        if (field.Type == TemplateFieldTypes.StaticText)
        {
            if (string.IsNullOrWhiteSpace(field.Content)) return null;
            return new(field.Key, null, Resolve(field.Content, context), field.Type, true, field.Order);
        }

        if (!data.Values.TryGetValue(field.Key!, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()))
            return null;

        var display = field.Type switch
        {
            TemplateFieldTypes.Boolean or TemplateFieldTypes.Checkbox => value.GetBoolean() ? "Yes" : "No",
            TemplateFieldTypes.Select or TemplateFieldTypes.Radio =>
                field.Options!.Single(option => option.Value == value.GetString()).Label ?? string.Empty,
            TemplateFieldTypes.Number => value.GetDecimal().ToString(CultureInfo.InvariantCulture),
            TemplateFieldTypes.Date => DateOnly.ParseExact(value.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => value.GetString() ?? string.Empty
        };
        return new(field.Key, Resolve(field.Label ?? string.Empty, context), Resolve(display, context),
            field.Type!, false, field.Order);
    }

    private string Resolve(string value, TemplateVariableContext? context) =>
        context is null ? value : variables.Resolve(value, context);
}
