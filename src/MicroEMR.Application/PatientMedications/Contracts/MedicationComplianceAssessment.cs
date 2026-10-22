using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MicroEMR.Application.PatientMedications.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class MedicationComplianceAssessment : IValidatableObject
{
    public static readonly string[] States = ["Compliant", "Not compliant", "Unknown", "Not assessed"];
    private string? state, explanation;
    private DateTimeOffset? assessmentAt;
    [JsonIgnore] public bool StateSpecified { get; private set; }
    [JsonIgnore] public bool ExplanationSpecified { get; private set; }
    [JsonIgnore] public bool AssessmentAtSpecified { get; private set; }
    [JsonIgnore] public string? State { get => state; set { state=value; StateSpecified=true; } }
    [JsonIgnore] public string? Explanation { get => explanation; set { explanation=value; ExplanationSpecified=true; } }
    [JsonIgnore] public DateTimeOffset? AssessmentAt { get => assessmentAt; set { assessmentAt=value; AssessmentAtSpecified=true; } }
    [JsonPropertyName("State"),JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)]
    public System.Text.Json.JsonElement StateInput {
        get => StateSpecified ? System.Text.Json.JsonSerializer.SerializeToElement(State) : default;
        set => State=Text(value);
    }
    [JsonPropertyName("Explanation"),JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)]
    public System.Text.Json.JsonElement ExplanationInput {
        get => ExplanationSpecified ? System.Text.Json.JsonSerializer.SerializeToElement(Explanation) : default;
        set => Explanation=Text(value);
    }
    [JsonPropertyName("AssessmentAt"),JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)]
    public System.Text.Json.JsonElement AssessmentAtInput {
        get => AssessmentAtSpecified ? System.Text.Json.JsonSerializer.SerializeToElement(AssessmentAt) : default;
        set { var reader=new System.Text.Json.Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(value.GetRawText()));reader.Read();AssessmentAt=new OffsetAssessmentTimeConverter().Read(ref reader,typeof(DateTimeOffset?),new()); }
    }
    private static string? Text(System.Text.Json.JsonElement value) => value.ValueKind switch {
        System.Text.Json.JsonValueKind.Null=>null,System.Text.Json.JsonValueKind.String=>value.GetString(),_=>throw new System.Text.Json.JsonException("Assessment fields must be text or null.")
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (State is not null && !States.Contains(State, StringComparer.Ordinal))
            yield return new("Select Compliant, Not compliant, Unknown, Not assessed or Not recorded.", [nameof(State)]);
        if (Explanation?.Length > 1000)
            yield return new("Compliance explanation must be 1,000 UTF-16 units or fewer.", [nameof(Explanation)]);
        if (StateSpecified && State is null && (!string.IsNullOrWhiteSpace(Explanation) || AssessmentAt.HasValue))
            yield return new("Choose an assessment state, or clear its explanation and assessment time.", [nameof(State)]);
    }

    public void EnsureValid()
    {
        var error = Validate(new(this)).FirstOrDefault();
        if (error is not null) throw new ArgumentException(error.ErrorMessage);
    }
}

public sealed class OffsetAssessmentTimeConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref System.Text.Json.Utf8JsonReader reader, Type type, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.Null) return null;
        if (reader.TokenType != System.Text.Json.JsonTokenType.String) throw new System.Text.Json.JsonException("Assessment time must include a UTC offset.");
        var text = reader.GetString()!;
        if (!System.Text.RegularExpressions.Regex.IsMatch(text, @"(?:Z|[+-]\d{2}:\d{2})$") || !DateTimeOffset.TryParse(text,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var value))
            throw new System.Text.Json.JsonException("Assessment time must include a UTC offset.");
        return value;
    }
    public override void Write(System.Text.Json.Utf8JsonWriter writer, DateTimeOffset? value, System.Text.Json.JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteStringValue(value.Value); else writer.WriteNullValue();
    }
}

public sealed class MedicationComplianceRevision : MedicationComplianceAssessment
{
    public long AssessorUserId { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public int RevisionNumber { get; set; }
}
