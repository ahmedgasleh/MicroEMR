namespace MicroEMR.Application.PatientPrescriptions;

public static class TargetDispensingFacilityName
{
    public const int MaximumLength = 200;

    // Explicit spacing set shared with the SQL boundary; controls are rejected before trimming.
    private static bool IsBoundarySpace(char value) => value is '\u0020' or '\u00a0' or '\u1680' or
        >= '\u2000' and <= '\u200a' or '\u202f' or '\u205f' or '\u3000';

    public static string? Normalize(string? value)
    {
        if (value is null) return null;
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current is <= '\u001f' or >= '\u007f' and <= '\u009f' or '\u2028' or '\u2029')
                throw new FacilityNameValidationException("Target dispensing facility name must be a single line without control characters.");
            if (char.IsHighSurrogate(current))
            {
                if (++index >= value.Length || !char.IsLowSurrogate(value[index]))
                    throw new FacilityNameValidationException("Target dispensing facility name contains invalid Unicode.");
            }
            else if (char.IsLowSurrogate(current))
                throw new FacilityNameValidationException("Target dispensing facility name contains invalid Unicode.");
        }
        var start = 0;
        var end = value.Length;
        while (start < end && IsBoundarySpace(value[start])) start++;
        while (end > start && IsBoundarySpace(value[end - 1])) end--;
        if (end - start > MaximumLength)
            throw new FacilityNameValidationException("Target dispensing facility name must be 200 UTF-16 code units or fewer; it will not be shortened.");
        return start == end ? null : value[start..end];
    }
}

public class FacilityNameValidationException(string message) : ArgumentException(message);
public sealed class FacilityNameReviewException() : FacilityNameValidationException(
    "Review the target dispensing facility name in Edit and save the draft before finalizing this correction.");
