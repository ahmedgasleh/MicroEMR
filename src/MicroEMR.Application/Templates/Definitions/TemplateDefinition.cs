using System.Text.Json.Serialization;

namespace MicroEMR.Application.Templates.Definitions;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class TemplateDefinition
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("sections")]
    public List<TemplateSectionDefinition>? Sections { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class TemplateSectionDefinition
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("key")] public string? Key { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("order")] public int Order { get; set; }
    [JsonPropertyName("fields")] public List<TemplateFieldDefinition>? Fields { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class TemplateFieldDefinition
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("key")] public string? Key { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("order")] public int Order { get; set; }
    [JsonPropertyName("required")] public bool Required { get; set; }
    [JsonPropertyName("defaultValue")] public string? DefaultValue { get; set; }
    [JsonPropertyName("options")] public List<TemplateFieldOption>? Options { get; set; }
    [JsonPropertyName("helpText")] public string? HelpText { get; set; }
    [JsonPropertyName("placeholder")] public string? Placeholder { get; set; }
    [JsonPropertyName("content")] public string? Content { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class TemplateFieldOption
{
    [JsonPropertyName("value")] public string? Value { get; set; }
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("order")] public int Order { get; set; }
}

public static class TemplateFieldTypes
{
    public const string StaticText = "StaticText";
    public const string Text = "Text";
    public const string TextArea = "TextArea";
    public const string Number = "Number";
    public const string Date = "Date";
    public const string Boolean = "Boolean";
    public const string Checkbox = "Checkbox";
    public const string Select = "Select";
    public const string Radio = "Radio";

    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.Ordinal)
    { StaticText, Text, TextArea, Number, Date, Boolean, Checkbox, Select, Radio };

    public static bool UsesOptions(string? value) => value is Select or Radio;
}

public static class TemplateDefinitionLimits
{
    public const int MaximumJsonBytes = 1_048_576;
    public const int MaximumSections = 50;
    public const int MaximumFieldsPerSection = 100;
    public const int MaximumTotalFields = 500;
    public const int MaximumOptionsPerField = 100;
    public const int MaximumIdLength = 100;
    public const int MaximumKeyLength = 100;
    public const int MaximumTitleLength = 200;
    public const int MaximumLabelLength = 200;
    public const int MaximumOptionValueLength = 100;
    public const int MaximumTextLength = 4_000;
}
