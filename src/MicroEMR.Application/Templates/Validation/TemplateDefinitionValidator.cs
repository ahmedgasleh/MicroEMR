using System.Text.RegularExpressions;
using MicroEMR.Application.Templates.Definitions;
using System.Globalization;

namespace MicroEMR.Application.Templates.Validation;

public sealed partial class TemplateDefinitionValidator : ITemplateDefinitionValidator
{
    public TemplateDefinitionValidationResult Validate(TemplateDefinition? definition)
    {
        var errors = new List<TemplateDefinitionValidationError>();
        if (definition is null) return Result(errors, "$", "DefinitionRequired", "A template definition is required.");
        if (definition.SchemaVersion != 1) Add(errors, "schemaVersion", "UnsupportedSchemaVersion", "Only schemaVersion 1 is supported.");
        if (definition.Sections is null) return Result(errors, "sections", "SectionsRequired", "Sections must be supplied.");
        if (definition.Sections.Count > TemplateDefinitionLimits.MaximumSections)
            Add(errors, "sections", "TooManySections", $"A template may contain at most {TemplateDefinitionLimits.MaximumSections} sections.");

        var sectionIds = new HashSet<string>(StringComparer.Ordinal);
        var sectionKeys = new HashSet<string>(StringComparer.Ordinal);
        var fieldIds = new HashSet<string>(StringComparer.Ordinal);
        var fieldKeys = new HashSet<string>(StringComparer.Ordinal);
        var totalFields = 0;

        for (var sectionIndex = 0; sectionIndex < definition.Sections.Count; sectionIndex++)
        {
            var section = definition.Sections[sectionIndex];
            var path = $"sections[{sectionIndex}]";
            ValidateIdentity(errors, section.Id, $"{path}.id", "Section", sectionIds);
            ValidateKey(errors, section.Key, $"{path}.key", "Section", sectionKeys);
            Required(errors, section.Title, $"{path}.title", "SectionTitleRequired", "Section title is required.", TemplateDefinitionLimits.MaximumTitleLength);
            NonNegative(errors, section.Order, $"{path}.order");
            if (section.Fields is null) { Add(errors, $"{path}.fields", "FieldsRequired", "Section fields must be supplied."); continue; }
            if (section.Fields.Count > TemplateDefinitionLimits.MaximumFieldsPerSection)
                Add(errors, $"{path}.fields", "TooManyFields", $"A section may contain at most {TemplateDefinitionLimits.MaximumFieldsPerSection} fields.");
            totalFields += section.Fields.Count;
            for (var fieldIndex = 0; fieldIndex < section.Fields.Count; fieldIndex++)
                ValidateField(errors, section.Fields[fieldIndex], $"{path}.fields[{fieldIndex}]", fieldIds, fieldKeys);
        }

        if (totalFields > TemplateDefinitionLimits.MaximumTotalFields)
            Add(errors, "sections", "TooManyTotalFields", $"A template may contain at most {TemplateDefinitionLimits.MaximumTotalFields} fields.");
        return new(errors);
    }

    private static void ValidateField(List<TemplateDefinitionValidationError> errors, TemplateFieldDefinition field,
        string path, HashSet<string> ids, HashSet<string> keys)
    {
        ValidateIdentity(errors, field.Id, $"{path}.id", "Field", ids);
        ValidateKey(errors, field.Key, $"{path}.key", "Field", keys);
        if (!TemplateFieldTypes.Supported.Contains(field.Type ?? string.Empty))
            Add(errors, $"{path}.type", "UnsupportedFieldType", $"Field type '{field.Type}' is not supported.");
        NonNegative(errors, field.Order, $"{path}.order");

        if (field.Type == TemplateFieldTypes.StaticText)
        {
            Required(errors, field.Content, $"{path}.content", "StaticTextContentRequired", "StaticText content is required.", TemplateDefinitionLimits.MaximumTextLength);
            if (field.Required) Add(errors, $"{path}.required", "StaticTextCannotBeRequired", "StaticText does not produce a value and cannot be required.");
        }
        else
        {
            Required(errors, field.Label, $"{path}.label", "FieldLabelRequired", "Field label is required.", TemplateDefinitionLimits.MaximumLabelLength);
        }

        Safe(errors, field.Label, $"{path}.label"); Safe(errors, field.Content, $"{path}.content");
        Safe(errors, field.HelpText, $"{path}.helpText"); Safe(errors, field.Placeholder, $"{path}.placeholder");
        Length(errors, field.DefaultValue, $"{path}.defaultValue", TemplateDefinitionLimits.MaximumTextLength);
        Length(errors, field.HelpText, $"{path}.helpText", TemplateDefinitionLimits.MaximumTextLength);
        Length(errors, field.Placeholder, $"{path}.placeholder", TemplateDefinitionLimits.MaximumTextLength);

        var usesOptions = TemplateFieldTypes.UsesOptions(field.Type);
        ValidateDefault(errors,field,path);
        if (usesOptions && (field.Options is null || field.Options.Count == 0))
            Add(errors, $"{path}.options", "OptionsRequired", "Select and Radio fields require at least one option.");
        if (!usesOptions && field.Options is { Count: > 0 })
            Add(errors, $"{path}.options", "OptionsNotAllowed", "Options are only allowed for Select and Radio fields.");
        if (field.Options is null) return;
        if (field.Options.Count > TemplateDefinitionLimits.MaximumOptionsPerField)
            Add(errors, $"{path}.options", "TooManyOptions", $"A field may contain at most {TemplateDefinitionLimits.MaximumOptionsPerField} options.");
        var values = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < field.Options.Count; i++)
        {
            var option = field.Options[i]; var optionPath = $"{path}.options[{i}]";
            Required(errors, option.Value, $"{optionPath}.value", "OptionValueRequired", "Option value is required.", TemplateDefinitionLimits.MaximumOptionValueLength);
            Required(errors, option.Label, $"{optionPath}.label", "OptionLabelRequired", "Option label is required.", TemplateDefinitionLimits.MaximumLabelLength);
            if (!string.IsNullOrWhiteSpace(option.Value) && !values.Add(option.Value))
                Add(errors, $"{optionPath}.value", "DuplicateOptionValue", $"Option value '{option.Value}' is already used by this field.");
            NonNegative(errors, option.Order, $"{optionPath}.order"); Safe(errors, option.Label, $"{optionPath}.label");
        }
    }

    private static void ValidateDefault(List<TemplateDefinitionValidationError> errors,TemplateFieldDefinition field,string path)
    {
        if(field.DefaultValue is null)return;
        var valid=field.Type switch
        {
            TemplateFieldTypes.StaticText=>false,
            TemplateFieldTypes.Number=>decimal.TryParse(field.DefaultValue,NumberStyles.Number,CultureInfo.InvariantCulture,out _),
            TemplateFieldTypes.Date=>DateOnly.TryParseExact(field.DefaultValue,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _),
            TemplateFieldTypes.Boolean or TemplateFieldTypes.Checkbox=>bool.TryParse(field.DefaultValue,out _),
            TemplateFieldTypes.Select or TemplateFieldTypes.Radio=>field.Options?.Any(x=>x.Value==field.DefaultValue)==true,
            _=>true
        };
        if(!valid)Add(errors,$"{path}.defaultValue","InvalidDefaultValue","Default value is not valid for the field type.");
    }

    private static void ValidateIdentity(List<TemplateDefinitionValidationError> errors, string? value, string path, string label, HashSet<string> values)
    {
        Required(errors, value, path, $"{label}IdRequired", $"{label} id is required.", TemplateDefinitionLimits.MaximumIdLength);
        if (!string.IsNullOrWhiteSpace(value) && !IdRegex().IsMatch(value)) Add(errors, path, "InvalidId", "IDs must start with a lowercase letter and contain only lowercase letters, digits, and hyphens.");
        if (!string.IsNullOrWhiteSpace(value) && !values.Add(value)) Add(errors, path, $"Duplicate{label}Id", $"{label} id '{value}' is already used.");
    }

    private static void ValidateKey(List<TemplateDefinitionValidationError> errors, string? value, string path, string label, HashSet<string> values)
    {
        Required(errors, value, path, $"{label}KeyRequired", $"{label} key is required.", TemplateDefinitionLimits.MaximumKeyLength);
        if (!string.IsNullOrWhiteSpace(value) && !KeyRegex().IsMatch(value)) Add(errors, path, "InvalidKey", "Keys must start with a lowercase letter and contain only ASCII letters and digits.");
        if (!string.IsNullOrWhiteSpace(value) && !values.Add(value)) Add(errors, path, $"Duplicate{label}Key", $"{label} key '{value}' is already used in this template.");
    }

    private static void Required(List<TemplateDefinitionValidationError> errors, string? value, string path, string code, string message, int maximum)
    { if (string.IsNullOrWhiteSpace(value)) Add(errors, path, code, message); else Length(errors, value, path, maximum); }
    private static void Length(List<TemplateDefinitionValidationError> errors, string? value, string path, int maximum)
    { if (value?.Length > maximum) Add(errors, path, "StringTooLong", $"Value may not exceed {maximum} characters."); }
    private static void NonNegative(List<TemplateDefinitionValidationError> errors, int value, string path)
    { if (value < 0) Add(errors, path, "InvalidOrder", "Order must be zero or greater."); }
    private static void Safe(List<TemplateDefinitionValidationError> errors, string? value, string path)
    { if (value is not null && ExecutableRegex().IsMatch(value)) Add(errors, path, "ExecutableContentNotAllowed", "Executable or event-handler content is not allowed."); }
    private static void Add(List<TemplateDefinitionValidationError> errors, string path, string code, string message) => errors.Add(new(path, code, message));
    private static TemplateDefinitionValidationResult Result(List<TemplateDefinitionValidationError> errors, string path, string code, string message) { Add(errors, path, code, message); return new(errors); }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,99}$", RegexOptions.CultureInvariant)] private static partial Regex IdRegex();
    [GeneratedRegex("^[a-z][A-Za-z0-9]{0,99}$", RegexOptions.CultureInvariant)] private static partial Regex KeyRegex();
    [GeneratedRegex("<\\s*script|javascript\\s*:|on(?:click|load|error|submit)\\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex ExecutableRegex();
}
