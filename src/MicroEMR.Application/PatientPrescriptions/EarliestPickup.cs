using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MicroEMR.Application.Tenancy;

namespace MicroEMR.Application.PatientPrescriptions;

[JsonConverter(typeof(EarliestPickupJsonConverter))]
public sealed record EarliestPickup(string Precision, string Value, string TimeZoneId)
{
    public string Display => Precision == "Day"
        ? $"{Value} (time not specified; {TimeZoneId})"
        : $"{Value.Replace('T', ' ')} ({TimeZoneId})";

    public void ValidateShape()
    {
        if (string.IsNullOrWhiteSpace(TimeZoneId) || TimeZoneId.Length > 100)
            throw new PickupValidationException("Pickup requires a clinical timezone.");
        if (string.IsNullOrEmpty(Value)) throw new PickupValidationException("Pickup requires a date or date/time.");
        var valid = Precision switch
        {
            "Day" => Value.Length == 10 && DateOnly.TryParseExact(Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "Minute" => Value.Length == 22 && DateTimeOffset.TryParseExact(Value, "yyyy-MM-dd'T'HH:mmzzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ => false
        };
        if (!valid) throw new PickupValidationException("Pickup requires a full date or minute-precision date/time with an explicit offset.");
    }
}

public sealed class EarliestPickupJsonConverter : JsonConverter<EarliestPickup>
{
    public override EarliestPickup Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Pickup must be an object or null.");
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name is not ("precision" or "value" or "timeZoneId" or "Precision" or "Value" or "TimeZoneId") ||
                property.Value.ValueKind != JsonValueKind.String || !fields.TryAdd(property.Name, property.Value.GetString()!))
                throw new JsonException("Pickup has invalid or duplicate fields.");
        }
        if (fields.Count != 3) throw new JsonException("Pickup requires precision, value and timezone.");
        var result = new EarliestPickup(fields["Precision"], fields["Value"], fields["TimeZoneId"]);
        try { result.ValidateShape(); } catch (ArgumentException e) { throw new JsonException(e.Message); }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, EarliestPickup value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Precision", value.Precision);
        writer.WriteString("Value", value.Value);
        writer.WriteString("TimeZoneId", value.TimeZoneId);
        writer.WriteEndObject();
    }
}

public sealed class PickupValidationException(string message) : ArgumentException(message);

public sealed record PickupTimeOptions(string TimeZoneId, IReadOnlyList<string> Offsets);

public sealed class PrescriptionPickupPolicy(ITenantContext context, ITenantCatalog catalog, ILogger<PrescriptionPickupPolicy> logger)
{
    public async Task<PickupTimeOptions> OptionsAsync(string? localTime, CancellationToken token = default)
    {
        var tenant = await catalog.GetByUidAsync(context.TenantUid, token);
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(tenant?.DefaultTimeZoneId ?? ""); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            logger.LogWarning("Clinical timezone is unavailable for pickup entry.");
            throw new PickupValidationException("The clinical timezone is unavailable. Leave pickup unchanged and contact your clinic administrator.");
        }
        return new(zone.Id, string.IsNullOrEmpty(localTime) ? [] : Offsets(zone, localTime));
    }

    public async Task ValidateAsync(EarliestPickup value, CancellationToken token = default)
    {
        value.ValidateShape();
        var options = await OptionsAsync(value.Precision == "Minute" ? value.Value[..16] : null, token);
        if (value.TimeZoneId != options.TimeZoneId)
            throw new PickupValidationException("Pickup timezone must match the selected tenant's clinical timezone.");
        if (value.Precision == "Minute" && !options.Offsets.Contains(value.Value[16..]))
            throw new PickupValidationException("Select a valid explicit pickup offset for this clinical local time.");
    }

    public static IReadOnlyList<string> Offsets(TimeZoneInfo zone, string localTime)
    {
        if (!DateTime.TryParseExact(localTime, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            throw new PickupValidationException("Pickup time must include a full date and minute-precision time.");
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) throw new PickupValidationException("This pickup time does not exist because of daylight saving. Choose another time.");
        var offsets = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local) : [zone.GetUtcOffset(local)];
        return offsets.OrderByDescending(x => x).Select(x => $"{(x < TimeSpan.Zero ? "-" : "+")}{x.Duration():hh\\:mm}").ToArray();
    }
}
