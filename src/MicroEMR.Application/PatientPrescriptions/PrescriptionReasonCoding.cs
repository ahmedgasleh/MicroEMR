using System.Text.Json;
using System.Text.Json.Serialization;

namespace MicroEMR.Application.PatientPrescriptions;

[JsonConverter(typeof(PrescriptionReasonCodingJsonConverter))]
public sealed record PrescriptionReasonCoding(string? System, string? Code, string? Version = null, string? Display = null)
{
    public static PrescriptionReasonCoding? Normalize(PrescriptionReasonCoding? value) => value is null ? null : new(
        Component(value.System, 200, true), Component(value.Code, 100, true),
        Component(value.Version, 100, false), Component(value.Display, 500, false));

    private static bool Boundary(char value) => value is '\u0020' or '\u00a0' or '\u1680' or
        >= '\u2000' and <= '\u200a' or '\u202f' or '\u205f' or '\u3000';

    private static string? Component(string? value, int maximum, bool required)
    {
        if (value is null)
        {
            if (required) throw new ReasonCodingValidationException("A coded reason requires both coding system and code.");
            return null;
        }
        for (var index = 0; index < value.Length; index++)
        {
            var code = value[index];
            if (code is <= '\u001f' or >= '\u007f' and <= '\u009f' or '\u2028' or '\u2029')
                throw new ReasonCodingValidationException("Coded reason components must be single-line text without control characters.");
            if (char.IsHighSurrogate(code))
            {
                if (++index >= value.Length || !char.IsLowSurrogate(value[index]))
                    throw new ReasonCodingValidationException("Coded reason contains invalid Unicode.");
            }
            else if (char.IsLowSurrogate(code)) throw new ReasonCodingValidationException("Coded reason contains invalid Unicode.");
        }
        var start = 0; var end = value.Length;
        while (start < end && Boundary(value[start])) start++;
        while (end > start && Boundary(value[end - 1])) end--;
        if (end - start > maximum) throw new ReasonCodingValidationException(
            "Coded reason limits are 200/100/100/500 UTF-16 units for system/code/version/display; values are not truncated.");
        if (start == end)
        {
            if (required) throw new ReasonCodingValidationException("A coded reason requires both coding system and code; use Clear coded reason to remove the group.");
            return null;
        }
        return value[start..end];
    }
}

public sealed class PrescriptionReasonCodingJsonConverter : JsonConverter<PrescriptionReasonCoding>
{
    public override PrescriptionReasonCoding Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Coded reason must be an object or null.");
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var key = property.Name.ToLowerInvariant();
            if (key is not ("system" or "code" or "version" or "display") || fields.ContainsKey(key))
                throw new JsonException("Unknown or duplicate coded reason component.");
            if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) throw new JsonException("Coded reason components must be text or null.");
            try { fields.Add(key, property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString()); }
            catch (InvalidOperationException) { throw new JsonException("Coded reason contains invalid Unicode."); }
        }
        return new(fields.GetValueOrDefault("system"), fields.GetValueOrDefault("code"), fields.GetValueOrDefault("version"), fields.GetValueOrDefault("display"));
    }

    public override void Write(Utf8JsonWriter writer, PrescriptionReasonCoding value, JsonSerializerOptions options)
    {
        writer.WriteStartObject(); writer.WriteString("system", value.System); writer.WriteString("code", value.Code);
        if (value.Version is not null) writer.WriteString("version", value.Version);
        if (value.Display is not null) writer.WriteString("display", value.Display);
        writer.WriteEndObject();
    }
}

public class ReasonCodingValidationException(string message) : ArgumentException(message);
public sealed class ReasonCodingReviewException() : ReasonCodingValidationException(
    "Review the coded reason in Edit and save the draft before finalizing this correction.");
