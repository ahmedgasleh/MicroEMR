using System.Text.RegularExpressions;

namespace MicroEMR.Application.Templates.Variables;

public sealed record TemplateVariableDescriptor(string Key, string Label, string Category);

public sealed record TemplateVariableContext(
    string PatientFullName,
    DateOnly PatientDateOfBirth,
    string? ProviderDisplayName,
    DateTime EncounterDateUtc,
    DateOnly CurrentDate);

public interface ITemplateVariableResolver
{
    IReadOnlyList<TemplateVariableDescriptor> Registry { get; }
    string Resolve(string text, TemplateVariableContext context);
}

public sealed partial class TemplateVariableResolver : ITemplateVariableResolver
{
    public IReadOnlyList<TemplateVariableDescriptor> Registry { get; } =
    [
        new("Patient.FullName", "Patient Full Name", "Patient"),
        new("Patient.DateOfBirth", "Patient Date of Birth", "Patient"),
        new("Provider.DisplayName", "Provider Display Name", "Provider"),
        new("Encounter.Date", "Encounter Date", "Encounter"),
        new("CurrentDate", "Current Date", "System")
    ];

    public string Resolve(string text, TemplateVariableContext context)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Patient.FullName"] = context.PatientFullName,
            ["Patient.DateOfBirth"] = context.PatientDateOfBirth.ToString("yyyy-MM-dd"),
            ["Provider.DisplayName"] = context.ProviderDisplayName ?? string.Empty,
            ["Encounter.Date"] = DateOnly.FromDateTime(context.EncounterDateUtc).ToString("yyyy-MM-dd"),
            ["CurrentDate"] = context.CurrentDate.ToString("yyyy-MM-dd")
        };
        return VariablePattern().Replace(text, match =>
        {
            var key = match.Groups[1].Value;
            return values.TryGetValue(key, out var value)
                ? value
                : throw new TemplateVariableResolutionException(key);
        });
    }

    [GeneratedRegex(@"\{\{([^{}]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex VariablePattern();
}

public sealed class TemplateVariableResolutionException(string key)
    : ArgumentException($"Template variable '{key}' is not registered.")
{
    public string Key { get; } = key;
}
