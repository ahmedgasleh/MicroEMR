using System.Text.Json;
using System.Text.Json.Serialization;

namespace MicroEMR.Application.PatientPrescriptions;

[JsonConverter(typeof(PrescriptionProtocolReferenceJsonConverter))]
public sealed record PrescriptionProtocolReference(string? System, string? Identifier)
{
    public static PrescriptionProtocolReference? Normalize(PrescriptionProtocolReference? value) => value is null ? null : new(
        Component(value.System, 200, true), Component(value.Identifier, 200, true));

    private static bool Boundary(char value) => value is '\u0020' or '\u00a0' or '\u1680' or
        >= '\u2000' and <= '\u200a' or '\u202f' or '\u205f' or '\u3000';

    private static string? Component(string? value, int maximum, bool required)
    {
        if (value is null)
        {
            if (required) throw new ProtocolReferenceValidationException("A protocol reference requires both protocol system and identifier.");
            return null;
        }
        for (var index = 0; index < value.Length; index++)
        {
            var code = value[index];
            if (code is <= '\u001f' or >= '\u007f' and <= '\u009f' or '\u2028' or '\u2029')
                throw new ProtocolReferenceValidationException("Protocol reference components must be single-line text without control characters.");
            if (char.IsHighSurrogate(code))
            {
                if (++index >= value.Length || !char.IsLowSurrogate(value[index]))
                    throw new ProtocolReferenceValidationException("Protocol reference contains invalid Unicode.");
            }
            else if (char.IsLowSurrogate(code)) throw new ProtocolReferenceValidationException("Protocol reference contains invalid Unicode.");
        }
        var start = 0; var end = value.Length;
        while (start < end && Boundary(value[start])) start++;
        while (end > start && Boundary(value[end - 1])) end--;
        if (end - start > maximum) throw new ProtocolReferenceValidationException(
            "Protocol reference limits are 200 UTF-16 units for each system/identifier component; values are not truncated.");
        if (start == end)
        {
            if (required) throw new ProtocolReferenceValidationException("A protocol reference requires both protocol system and identifier; use Clear protocol reference to remove the group.");
            return null;
        }
        return value[start..end];
    }
}

public sealed class PrescriptionProtocolReferenceJsonConverter : JsonConverter<PrescriptionProtocolReference>
{
    public override PrescriptionProtocolReference Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Protocol reference must be an object or null.");
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var key = property.Name.ToLowerInvariant();
            if (key is not ("system" or "identifier") || fields.ContainsKey(key))
                throw new JsonException("Unknown or duplicate protocol reference component.");
            if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) throw new JsonException("Protocol reference components must be text or null.");
            try { fields.Add(key, property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString()); }
            catch (InvalidOperationException) { throw new JsonException("Protocol reference contains invalid Unicode."); }
        }
        return new(fields.GetValueOrDefault("system"), fields.GetValueOrDefault("identifier"));
    }

    public override void Write(Utf8JsonWriter writer, PrescriptionProtocolReference value, JsonSerializerOptions options)
    {
        writer.WriteStartObject(); writer.WriteString("system", value.System); writer.WriteString("identifier", value.Identifier);
        writer.WriteEndObject();
    }
}

public class ProtocolReferenceValidationException(string message) : ArgumentException(message);
public sealed class ProtocolReferenceReviewException() : ProtocolReferenceValidationException(
    "Review the protocol reference in Edit and save the draft before finalizing this correction.");
