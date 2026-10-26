namespace MicroEMR.Application.PatientPrescriptions;

public static class TargetDispensingFacilityAddress
{
    public const int MaximumLength = 1000;

    // Same explicit boundary spacing as the supplied name, plus canonical address line breaks.
    private static bool IsBoundarySpace(char value) => value is '\n' or '\u0020' or '\u00a0' or '\u1680' or
        >= '\u2000' and <= '\u200a' or '\u202f' or '\u205f' or '\u3000';

    public static string? Normalize(string? value)
    {
        if (value is null) return null;
        value = value.Replace("\r\n", "\n").Replace('\r', '\n');
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if ((current <= '\u001f' && current != '\n') || current is >= '\u007f' and <= '\u009f' or '\u2028' or '\u2029')
                throw new FacilityAddressValidationException("Target dispensing facility address permits line breaks but not tabs or other control characters.");
            if (char.IsHighSurrogate(current))
            {
                if (++index >= value.Length || !char.IsLowSurrogate(value[index]))
                    throw new FacilityAddressValidationException("Target dispensing facility address contains invalid Unicode.");
            }
            else if (char.IsLowSurrogate(current))
                throw new FacilityAddressValidationException("Target dispensing facility address contains invalid Unicode.");
        }
        var start = 0;
        var end = value.Length;
        while (start < end && IsBoundarySpace(value[start])) start++;
        while (end > start && IsBoundarySpace(value[end - 1])) end--;
        if (end - start > MaximumLength)
            throw new FacilityAddressValidationException("Target dispensing facility address must be 1,000 UTF-16 code units or fewer; it will not be shortened.");
        return start == end ? null : value[start..end];
    }
}

public class FacilityAddressValidationException(string message) : ArgumentException(message);
public sealed class FacilityAddressReviewException() : FacilityAddressValidationException(
    "Review the target dispensing facility address in Edit and save the draft before finalizing this correction.");
