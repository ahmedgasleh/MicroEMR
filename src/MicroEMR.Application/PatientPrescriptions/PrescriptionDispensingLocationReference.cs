using System.Text.Json;
using System.Text.Json.Serialization;

namespace MicroEMR.Application.PatientPrescriptions;

/// <summary>
/// Optional user-supplied dispensing location reference. System identifies the namespace/source;
/// Identifier is opaque within it. The pair alone does not establish directory resolution or a match
/// to independently entered facility name/address. Each component permits 200 UTF-16 units after boundary trimming.
/// Omit dispensingLocationReference to preserve on Edit (absent on Create), send null to clear both,
/// or supply a complete object to replace both atomically. Partial or invalid groups reject the save.
/// </summary>
[JsonConverter(typeof(PrescriptionDispensingLocationReferenceJsonConverter))]
public sealed record PrescriptionDispensingLocationReference(string? System, string? Identifier)
{
    public static PrescriptionDispensingLocationReference? Normalize(PrescriptionDispensingLocationReference? value) => value is null ? null : new(
        Component(value.System, 200, true), Component(value.Identifier, 200, true));

    private static bool Boundary(char value) => value is '\u0020' or '\u00a0' or '\u1680' or
        >= '\u2000' and <= '\u200a' or '\u202f' or '\u205f' or '\u3000';

    private static string? Component(string? value, int maximum, bool required)
    {
        if (value is null)
        {
            if (required) throw new DispensingLocationReferenceValidationException("A dispensing location reference requires both dispensing location system and identifier.");
            return null;
        }
        for (var index = 0; index < value.Length; index++)
        {
            var code = value[index];
            if (code is <= '\u001f' or >= '\u007f' and <= '\u009f' or '\u2028' or '\u2029')
                throw new DispensingLocationReferenceValidationException("Dispensing location reference components must be single-line text without control characters.");
            if (char.IsHighSurrogate(code))
            {
                if (++index >= value.Length || !char.IsLowSurrogate(value[index]))
                    throw new DispensingLocationReferenceValidationException("Dispensing location reference contains invalid Unicode.");
            }
            else if (char.IsLowSurrogate(code)) throw new DispensingLocationReferenceValidationException("Dispensing location reference contains invalid Unicode.");
        }
        var start = 0; var end = value.Length;
        while (start < end && Boundary(value[start])) start++;
        while (end > start && Boundary(value[end - 1])) end--;
        if (end - start > maximum) throw new DispensingLocationReferenceValidationException(
            "Dispensing location reference limits are 200 UTF-16 units for each system/identifier component; values are not truncated.");
        if (start == end)
        {
            if (required) throw new DispensingLocationReferenceValidationException("A dispensing location reference requires both dispensing location system and identifier; use Clear dispensing location reference to remove the group.");
            return null;
        }
        return value[start..end];
    }
}

public sealed class PrescriptionDispensingLocationReferenceJsonConverter : JsonConverter<PrescriptionDispensingLocationReference>
{
    public override PrescriptionDispensingLocationReference Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Dispensing location reference must be an object or null.");
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var key = property.Name.ToLowerInvariant();
            if (key is not ("system" or "identifier") || fields.ContainsKey(key))
                throw new JsonException("Unknown or duplicate dispensing location reference component.");
            if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) throw new JsonException("Dispensing location reference components must be text or null.");
            try { fields.Add(key, property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString()); }
            catch (InvalidOperationException) { throw new JsonException("Dispensing location reference contains invalid Unicode."); }
        }
        return new(fields.GetValueOrDefault("system"), fields.GetValueOrDefault("identifier"));
    }

    public override void Write(Utf8JsonWriter writer, PrescriptionDispensingLocationReference value, JsonSerializerOptions options)
    {
        writer.WriteStartObject(); writer.WriteString("system", value.System); writer.WriteString("identifier", value.Identifier);
        writer.WriteEndObject();
    }
}

public class DispensingLocationReferenceValidationException(string message) : ArgumentException(message);
public sealed class DispensingLocationReferenceReviewException() : DispensingLocationReferenceValidationException(
    "Review the dispensing location reference in Edit and save the draft before finalizing this correction.");
