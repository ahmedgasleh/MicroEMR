using System.Text.Json;
using MicroEMR.Application.Templates.Definitions;
using MicroEMR.Application.Templates.Runtime;

namespace MicroEMR.Application.PatientEncounters;

// Baseline values remain stored unchanged. Only explicitly field-linked narrative is composed.
public static class EncounterFieldComposition
{
    public static string Compose(string? baselineJson, IReadOnlyList<EncounterContributionResponse> contributions)
    {
        TemplateInstanceData? data;
        try { data = JsonSerializer.Deserialize<TemplateInstanceData>(baselineJson ?? "{\"schemaVersion\":1,\"values\":{}}"); }
        catch (JsonException) { return baselineJson!; } // Reads retain the baseline; sign validation still rejects malformed values.
        if (data is null) return baselineJson ?? "null";
        foreach (var group in contributions.Where(c => c.FieldKey is not null && c.ValueKind == "Narrative").GroupBy(c => c.FieldKey!))
        {
            var parts = new List<string>();
            if (data.Values.TryGetValue(group.Key, out var prior) && prior.ValueKind == JsonValueKind.String)
                parts.Add(prior.GetString()!);
            parts.AddRange(group.Select(c => c.Content));
            data.Values[group.Key] = JsonSerializer.SerializeToElement(string.Join("\n\n", parts.Where(p => !string.IsNullOrEmpty(p))));
        }
        return JsonSerializer.Serialize(data);
    }

    public static string ApplyFieldPatch(string? baselineJson, string nextJson, IReadOnlyList<string> keys)
    {
        var baseline = JsonSerializer.Deserialize<TemplateInstanceData>(baselineJson ?? "{\"schemaVersion\":1,\"values\":{}}")!;
        var next = JsonSerializer.Deserialize<TemplateInstanceData>(nextJson)!;
        foreach (var key in keys)
        {
            if (next.Values.TryGetValue(key, out var value)) baseline.Values[key] = value;
            else baseline.Values.Remove(key);
        }
        return JsonSerializer.Serialize(baseline);
    }

    public static string NormalizeNumbers(string json)
    {
        var data = JsonSerializer.Deserialize<TemplateInstanceData>(json)!;
        foreach (var pair in data.Values.ToArray())
            if (pair.Value.ValueKind == JsonValueKind.Number && pair.Value.TryGetDecimal(out var number))
            {
                using var parsed = JsonDocument.Parse(number.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture));
                data.Values[pair.Key] = parsed.RootElement.Clone();
            }
        return JsonSerializer.Serialize(data);
    }

    public static void EnsureNarrativeUnchanged(TemplateDefinition definition, string? baselineJson, string nextJson)
    {
        var prior = JsonSerializer.Deserialize<TemplateInstanceData>(baselineJson ?? "{\"schemaVersion\":1,\"values\":{}}")!;
        var next = JsonSerializer.Deserialize<TemplateInstanceData>(nextJson)!;
        foreach (var field in definition.Sections!.SelectMany(s => s.Fields!).Where(f => f.Type is "Text" or "TextArea"))
        {
            prior.Values.TryGetValue(field.Key!, out var oldValue);
            next.Values.TryGetValue(field.Key!, out var newValue);
            string? Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (!string.Equals(Text(oldValue), Text(newValue), StringComparison.Ordinal))
                throw new EncounterNoteNotEditableException("Existing narrative is read-only. Use an attributed contribution for new text.");
        }
    }
}
