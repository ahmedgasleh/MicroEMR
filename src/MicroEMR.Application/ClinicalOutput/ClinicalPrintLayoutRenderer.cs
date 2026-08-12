using System.Globalization;
using System.Net;
using System.Text;

namespace MicroEMR.Application.ClinicalOutput;

public sealed record ClinicalPrintClinic(
    string Name, string? AddressLine1, string? AddressLine2, string? City,
    string? Province, string? PostalCode, string? Phone, string? Fax, string? Email);

public sealed record ClinicalPrintPatient(
    string FullName, DateOnly DateOfBirth, string? HealthCardNumber,
    string? HealthCardVersion, string? ChartNumber);

public sealed record ClinicalPrintRecord(
    string Kind, string Title, string Type, DateTime DateUtc, string? Provider);

public sealed record ClinicalPrintAuthorship(
    string? AuthorLabel, string? AuthorName, DateTime? AuthoredAtUtc,
    string? SignedBy, DateTime? SignedAtUtc);

public sealed record ClinicalPrintContext(
    ClinicalPrintClinic Clinic,
    ClinicalPrintPatient Patient,
    ClinicalPrintRecord Record,
    ClinicalPrintAuthorship Authorship,
    string TimeZoneId);

public interface IClinicalPrintLayoutRenderer
{
    string Render(ClinicalPrintContext context, string clinicalHtml);
}

public sealed class ClinicalPrintLayoutRenderer : IClinicalPrintLayoutRenderer
{
    public string Render(ClinicalPrintContext context, string clinicalHtml)
    {
        ArgumentNullException.ThrowIfNull(context);
        var html = new StringBuilder("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>")
            .Append(E(context.Record.Title)).Append("</title><style>")
            .Append(Css).Append("</style></head><body><main class=\"clinical-print-output\">");

        html.Append("<header class=\"clinical-print-header\"><div class=\"clinic-identity\"><div class=\"clinic-name\">")
            .Append(E(context.Clinic.Name)).Append("</div>");
        AppendLine(html, context.Clinic.AddressLine1);
        AppendLine(html, context.Clinic.AddressLine2);
        AppendLine(html, JoinLocation(context.Clinic.City, context.Clinic.Province, context.Clinic.PostalCode));
        AppendContactLine(html, context.Clinic);
        html.Append("</div><dl class=\"clinical-context-grid\">");
        AppendPair(html, "Patient", context.Patient.FullName);
        AppendPair(html, "DOB", context.Patient.DateOfBirth.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture));
        AppendPair(html, "Health Card", JoinNonEmpty(" ", context.Patient.HealthCardNumber, context.Patient.HealthCardVersion));
        AppendPair(html, "Chart", context.Patient.ChartNumber);
        AppendPair(html, context.Record.Kind, context.Record.Title);
        AppendPair(html, "Type", context.Record.Type);
        AppendPair(html, "Date", FormatDate(context.Record.DateUtc, context.TimeZoneId));
        AppendPair(html, "Provider", context.Record.Provider);
        html.Append("</dl></header><section class=\"clinical-print-body\">").Append(clinicalHtml)
            .Append("</section><footer class=\"clinical-print-footer\">");

        if (!string.IsNullOrWhiteSpace(context.Authorship.SignedBy) && context.Authorship.SignedAtUtc.HasValue)
        {
            AppendPair(html, "Electronically signed by", context.Authorship.SignedBy);
            AppendPair(html, "Signed", FormatDateTime(context.Authorship.SignedAtUtc.Value, context.TimeZoneId));
        }
        else
        {
            AppendPair(html, context.Authorship.AuthorLabel ?? "Created by", context.Authorship.AuthorName);
            if (context.Authorship.AuthoredAtUtc.HasValue)
                AppendPair(html, "Created", FormatDateTime(context.Authorship.AuthoredAtUtc.Value, context.TimeZoneId));
        }
        return html.Append("</footer></main></body></html>").ToString();
    }

    private static readonly string Css = """
        @page { size: Letter; margin: 0.65in; }
        * { box-sizing: border-box; }
        body { margin: 0; color: #202124; font: 10.5pt Arial, Helvetica, sans-serif; line-height: 1.4; }
        .clinical-print-header { margin-bottom: 18pt; break-inside: avoid; }
        .clinic-identity { padding-bottom: 8pt; border-bottom: 2px solid #333; }
        .clinic-name { margin-bottom: 2pt; font-size: 15pt; font-weight: 700; text-transform: uppercase; }
        .clinic-line, .clinic-contact { color: #444; }
        .clinical-context-grid { display: grid; grid-template-columns: max-content 1fr max-content 1fr; gap: 3pt 8pt; margin: 9pt 0 0; }
        .clinical-context-grid dt, .clinical-print-footer dt { font-weight: 700; }
        .clinical-context-grid dd, .clinical-print-footer dd { margin: 0; overflow-wrap: anywhere; }
        .clinical-print-body { padding-top: 12pt; border-top: 1px solid #888; }
        .clinical-print-footer { margin-top: 22pt; padding-top: 9pt; border-top: 1px solid #888; break-inside: avoid; }
        .clinical-print-footer dt { display: inline; }
        .clinical-print-footer dd { display: inline; margin-left: 4pt; }
        .clinical-print-footer dd::after { content: ''; display: block; margin-bottom: 3pt; }
        .template-section { margin: 0 0 18pt; }
        .template-section-title { margin: 0 0 9pt; padding-bottom: 4pt; border-bottom: 1px solid #888; font-size: 14pt; break-after: avoid; }
        .template-field, .template-static-text { margin: 0 0 9pt; break-inside: avoid; overflow-wrap: anywhere; }
        .template-field-label { display: block; margin-bottom: 2pt; }
        table { width: 100%; border-collapse: collapse; }
        th, td { padding: 4pt; border: 1px solid #999; text-align: left; }
        tr { break-inside: avoid; }
        @media (max-width: 600px) { .clinical-context-grid { grid-template-columns: max-content 1fr; } }
        """;

    private static void AppendLine(StringBuilder html, string? value)
    { if (!string.IsNullOrWhiteSpace(value)) html.Append("<div class=\"clinic-line\">").Append(E(value)).Append("</div>"); }

    private static void AppendContactLine(StringBuilder html, ClinicalPrintClinic clinic)
    {
        var values = new[] { Prefix("Tel", clinic.Phone), Prefix("Fax", clinic.Fax), clinic.Email }
            .Where(value => !string.IsNullOrWhiteSpace(value));
        var line = string.Join(" · ", values);
        if (line.Length > 0) html.Append("<div class=\"clinic-contact\">").Append(E(line)).Append("</div>");
    }

    private static void AppendPair(StringBuilder html, string label, string? value)
    { if (!string.IsNullOrWhiteSpace(value)) html.Append("<dt>").Append(E(label)).Append(":</dt><dd>").Append(E(value)).Append("</dd>"); }

    private static string? Prefix(string label, string? value) => string.IsNullOrWhiteSpace(value) ? null : $"{label}: {value}";
    private static string? JoinLocation(params string?[] values) => JoinNonEmpty(", ", values);
    private static string? JoinNonEmpty(string separator, params string?[] values)
    { var result = string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value))); return result.Length == 0 ? null : result; }
    private static string FormatDate(DateTime utc, string zone) => ToZone(utc, zone).ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
    private static string FormatDateTime(DateTime utc, string zone) => ToZone(utc, zone).ToString("MMMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);
    private static DateTime ToZone(DateTime value, string zone)
    { var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc); try { return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(zone)); } catch (TimeZoneNotFoundException) { return utc; } catch (InvalidTimeZoneException) { return utc; } }
    private static string E(string value) => WebUtility.HtmlEncode(value);
}
