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
    private bool? paperCopyRequiredBeforeDispense;
    private int? minimumDispenseIntervalDays;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool? PaperCopyRequiredBeforeDispense { get => paperCopyRequiredBeforeDispense; set { paperCopyRequiredBeforeDispense=value; PaperCopyRequiredBeforeDispenseSpecified=true; } }
    [System.Text.Json.Serialization.JsonIgnore] public bool PaperCopyRequiredBeforeDispenseSpecified {get;private set;}
    [System.Text.Json.Serialization.JsonPropertyName("PaperCopyRequiredBeforeDispense"),System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public System.Text.Json.JsonElement PaperCopyRequiredBeforeDispenseInput {
        get => PaperCopyRequiredBeforeDispenseSpecified ? System.Text.Json.JsonSerializer.SerializeToElement(PaperCopyRequiredBeforeDispense) : default;
        set { if(value.ValueKind is not (System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.Null)) throw new System.Text.Json.JsonException("Paper copy requirement permits Yes or Not recorded only."); PaperCopyRequiredBeforeDispense=value.ValueKind==System.Text.Json.JsonValueKind.True?true:null; }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public int? MinimumDispenseIntervalDays {get=>minimumDispenseIntervalDays;set{minimumDispenseIntervalDays=value;MinimumDispenseIntervalDaysSpecified=true;}}
    [System.Text.Json.Serialization.JsonIgnore] public bool MinimumDispenseIntervalDaysSpecified {get;private set;}
    [System.Text.Json.Serialization.JsonPropertyName("MinimumDispenseIntervalDays"),System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public System.Text.Json.JsonElement MinimumDispenseIntervalDaysInput {
        get => MinimumDispenseIntervalDaysSpecified ? System.Text.Json.JsonSerializer.SerializeToElement(MinimumDispenseIntervalDays) : default;
        set { if(value.ValueKind==System.Text.Json.JsonValueKind.Null){MinimumDispenseIntervalDays=null;return;} if(value.ValueKind!=System.Text.Json.JsonValueKind.Number || !value.TryGetInt32(out var days) || days<=0) throw new System.Text.Json.JsonException("Minimum dispense interval must be positive whole days within Int32 storage."); MinimumDispenseIntervalDays=days; }
    }
    public string? ClinicalError() => PaperCopyRequiredBeforeDispense is false ? "Paper copy requirement permits Yes or Not recorded only." : MinimumDispenseIntervalDays is <=0 ? "Minimum dispense interval must be positive whole days." : null;
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
