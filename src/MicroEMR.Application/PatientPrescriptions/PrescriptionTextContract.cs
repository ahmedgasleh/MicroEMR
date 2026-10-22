namespace MicroEMR.Application.PatientPrescriptions;

public static class PrescriptionTextContract
{
    public const int MaximumLength = 300;
    public static string? Error(string? value, bool required = true)
    {
        if (required && string.IsNullOrWhiteSpace(value)) return "Drug Description is required.";
        if (value?.Length > MaximumLength) return "Prescription text must be 300 UTF-16 code units or fewer.";
        if (value?.IndexOfAny(['\r', '\n', '\v', '\f', '\u0085', '\u2028', '\u2029']) >= 0)
            return "Prescription text must be a single line.";
        return null;
    }
    public static void Validate(string? value, bool required = true)
    {
        if (Error(value, required) is { } error) throw new ArgumentException(error);
    }
}

// An omitted group preserves treatment type; an empty group clears it.
// Substitution intent tracks its own presence so an older group cannot erase it.
public sealed class PrescriptionOrderFields
{
    public string? TreatmentType { get; set; }
    private bool? substitutionProhibited;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool? SubstitutionProhibited { get => substitutionProhibited; set { substitutionProhibited=value; SubstitutionProhibitedSpecified=true; } }
    [System.Text.Json.Serialization.JsonIgnore] public bool SubstitutionProhibitedSpecified { get; private set; }
    [System.Text.Json.Serialization.JsonPropertyName("SubstitutionProhibited")]
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public System.Text.Json.JsonElement SubstitutionProhibitedInput
    {
        get => SubstitutionProhibitedSpecified ? System.Text.Json.JsonSerializer.SerializeToElement(SubstitutionProhibited) : default;
        set
        {
            if(value.ValueKind is not (System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False or System.Text.Json.JsonValueKind.Null))
                throw new System.Text.Json.JsonException("Substitution prohibition must be a boolean or null.");
            SubstitutionProhibited=value.ValueKind==System.Text.Json.JsonValueKind.Null?null:value.GetBoolean();
        }
    }
}
