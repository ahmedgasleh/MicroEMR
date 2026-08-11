using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Application.Templates.Serialization;

namespace MicroEMR.Application.Templates.Runtime;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class TemplateInstanceData
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
    [JsonPropertyName("values")] public Dictionary<string, JsonElement> Values { get; set; } = new(StringComparer.Ordinal);
}

public sealed record TemplateInstanceValidationError(string Path, string Code, string Message);
public sealed record TemplateInstanceProcessingResult(bool IsValid, TemplateInstanceData? Data, string? Json,
    IReadOnlyList<TemplateInstanceValidationError> Errors);

public interface ITemplateInstanceRuntime
{
    TemplateInstanceProcessingResult Process(TemplateDefinition definition, string? dataJson);
    TemplateInstanceProcessingResult CreateInitial(TemplateDefinition definition);
    string RenderSnapshot(TemplateDefinition definition, TemplateInstanceData data);
}

public sealed class TemplateInstanceRuntime(ITemplateDefinitionSerializer definitions) : ITemplateInstanceRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false
    };

    public TemplateInstanceProcessingResult Process(TemplateDefinition definition, string? dataJson)
    {
        var schema = definitions.Process(definition);
        if (!schema.IsValid)
            return Invalid(schema.Errors.Select(x => new TemplateInstanceValidationError(x.Path, x.Code, x.Message)));
        if (string.IsNullOrWhiteSpace(dataJson)) return Invalid([new("$", "DataRequired", "Structured document data is required.")]);

        TemplateInstanceData? data;
        try { data = JsonSerializer.Deserialize<TemplateInstanceData>(dataJson, JsonOptions); }
        catch (JsonException exception) { return Invalid([new(exception.Path ?? "$", "MalformedData", "Structured document data is malformed.")]); }
        if (data is null || data.SchemaVersion != definition.SchemaVersion)
            return Invalid([new("schemaVersion", "UnsupportedSchemaVersion", "The document data schema version does not match the template.")]);

        var fields = definition.Sections!.SelectMany(x => x.Fields!).ToDictionary(x => x.Key!, StringComparer.Ordinal);
        var errors = new List<TemplateInstanceValidationError>();
        foreach (var key in data.Values.Keys.Where(key => !fields.ContainsKey(key)))
            errors.Add(new($"values.{key}", "UnknownField", "The value does not belong to this template version."));

        foreach (var field in fields.Values)
        {
            var path = $"values.{field.Key}";
            var present = data.Values.TryGetValue(field.Key!, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
            if (field.Type == TemplateFieldTypes.StaticText)
            {
                if (present) errors.Add(new(path, "StaticTextValue", "Instructional text cannot store a patient value."));
                continue;
            }
            if (!present)
            {
                if (field.Required) errors.Add(new(path, "Required", $"{field.Label} is required."));
                continue;
            }
            var valid = field.Type switch
            {
                TemplateFieldTypes.Text or TemplateFieldTypes.TextArea => value.ValueKind == JsonValueKind.String,
                TemplateFieldTypes.Number => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
                TemplateFieldTypes.Date => value.ValueKind == JsonValueKind.String && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                TemplateFieldTypes.Boolean or TemplateFieldTypes.Checkbox => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                TemplateFieldTypes.Select or TemplateFieldTypes.Radio => value.ValueKind == JsonValueKind.String && field.Options!.Any(x => x.Value == value.GetString()),
                _ => false
            };
            if (!valid) errors.Add(new(path, "InvalidValue", $"{field.Label} has an invalid value."));
            else if (field.Required && value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()))
                errors.Add(new(path, "Required", $"{field.Label} is required."));
        }
        return errors.Count > 0 ? Invalid(errors) : new(true, data, JsonSerializer.Serialize(data, JsonOptions), []);
    }

    public TemplateInstanceProcessingResult CreateInitial(TemplateDefinition definition)
    {
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var field in definition.Sections?.SelectMany(x => x.Fields ?? []) ?? [])
        {
            if (field.Type == TemplateFieldTypes.StaticText || field.DefaultValue is null) continue;
            var defaultJson = field.Type switch
            {
                TemplateFieldTypes.Number when decimal.TryParse(field.DefaultValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) => JsonSerializer.Serialize(number),
                TemplateFieldTypes.Boolean or TemplateFieldTypes.Checkbox when bool.TryParse(field.DefaultValue, out var boolean) => boolean ? "true" : "false",
                _ => JsonSerializer.Serialize(field.DefaultValue)
            };
            using var parsed = JsonDocument.Parse(defaultJson);
            values[field.Key!] = parsed.RootElement.Clone();
        }
        var data = new TemplateInstanceData { SchemaVersion = definition.SchemaVersion, Values = values };
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var validation = Process(definition, json);
        var nonRequiredErrors = validation.Errors.Where(x => x.Code != "Required").ToArray();
        return nonRequiredErrors.Length == 0 ? new(true, data, json, []) : Invalid(nonRequiredErrors);
    }

    public string RenderSnapshot(TemplateDefinition definition, TemplateInstanceData data)
    {
        var output = new StringBuilder();
        foreach (var section in definition.Sections!.OrderBy(x => x.Order))
        {
            if (output.Length > 0) output.Append('\n');
            output.Append(section.Title).Append('\n');
            output.Append(new string('-', section.Title!.Length)).Append('\n');
            foreach (var field in section.Fields!.OrderBy(x => x.Order))
            {
                if (field.Type == TemplateFieldTypes.StaticText) { if (!string.IsNullOrWhiteSpace(field.Content)) output.Append(field.Content).Append('\n'); continue; }
                if (!data.Values.TryGetValue(field.Key!, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined || value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())) continue;
                var display = field.Type switch
                {
                    TemplateFieldTypes.Boolean or TemplateFieldTypes.Checkbox => value.GetBoolean() ? "Yes" : "No",
                    TemplateFieldTypes.Select or TemplateFieldTypes.Radio => field.Options!.First(x => x.Value == value.GetString()).Label,
                    TemplateFieldTypes.Number => value.GetRawText(),
                    _ => value.GetString()
                };
                output.Append(field.Label).Append('\n');
                output.Append(display).Append('\n').Append('\n');
            }
        }
        return output.ToString().TrimEnd();
    }

    private static TemplateInstanceProcessingResult Invalid(IEnumerable<TemplateInstanceValidationError> errors) => new(false, null, null, errors.ToArray());
}

public sealed class TemplateInstanceValidationException(IReadOnlyList<TemplateInstanceValidationError> errors)
    : ArgumentException("Structured document data is invalid.")
{
    public IReadOnlyList<TemplateInstanceValidationError> Errors { get; } = errors;
}
