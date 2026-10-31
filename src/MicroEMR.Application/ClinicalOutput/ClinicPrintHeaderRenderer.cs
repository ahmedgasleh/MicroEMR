using System.Net;
using System.Text;
using MicroEMR.Application.ClinicConfiguration;

namespace MicroEMR.Application.ClinicalOutput;

public sealed record ClinicPrintHeader(ClinicalPrintClinic Clinic, string DocumentTitle, ClinicLogo? Logo = null, string? Country = null);

// Standalone fragment and CSS: future generators opt in; existing/finalized outputs are untouched.
public static class ClinicPrintHeaderRenderer
{
    public const string Css = """
        @page { size: Letter; margin: 0.65in; }
        body { color: #202124; font: 10.5pt Arial, Helvetica, sans-serif; line-height: 1.4; }
        .clinic-letterhead { margin: 0 0 18pt; break-inside: avoid; overflow-wrap: anywhere; }
        .clinic-letterhead-identity { display: flex; gap: 16pt; align-items: center; padding-bottom: 10pt; border-bottom: 1px solid #888; }
        .clinic-letterhead-logo { max-width: 110pt; max-height: 65pt; width: auto; height: auto; object-fit: contain; flex: 0 0 auto; }
        .clinic-letterhead-name { font-size: 16pt; font-weight: bold; }
        .clinic-letterhead-title { font-size: 13pt; margin: 10pt 0 0; }
        @media print { .clinic-letterhead { print-color-adjust: exact; } }
        """;

    public static string Render(ClinicPrintHeader header)
    {
        var html = new StringBuilder("<header class=\"clinic-letterhead\"><div class=\"clinic-letterhead-identity\">");
        if (header.Logo is { PngBytes.Length: > 0 } logo)
            html.Append("<img class=\"clinic-letterhead-logo\" alt=\"Clinic logo\" src=\"data:image/png;base64,")
                .Append(Convert.ToBase64String(logo.PngBytes)).Append("\" />");
        html.Append("<div><div class=\"clinic-letterhead-name\">").Append(E(header.Clinic.Name)).Append("</div>");
        Line(header.Clinic.AddressLine1); Line(header.Clinic.AddressLine2);
        Line(string.Join(", ", new[] { header.Clinic.City, header.Clinic.Province, header.Clinic.PostalCode }.Where(Has)));
        Line(header.Country);
        Line(string.Join(" · ", new[] { Prefix("Tel", header.Clinic.Phone), Prefix("Fax", header.Clinic.Fax), header.Clinic.Email }.Where(Has)));
        return html.Append("</div></div><h1 class=\"clinic-letterhead-title\">").Append(E(header.DocumentTitle)).Append("</h1></header>").ToString();

        void Line(string? text) { if (Has(text)) html.Append("<div>").Append(E(text!)).Append("</div>"); }
    }

    public static string RenderDocument(ClinicPrintHeader header) =>
        "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Clinic print header preview</title><style>" +
        Css + "</style></head><body>" + Render(header) + "</body></html>";

    private static string E(string text) => WebUtility.HtmlEncode(text);
    private static bool Has(string? text) => !string.IsNullOrWhiteSpace(text);
    private static string? Prefix(string label, string? text) => Has(text) ? $"{label}: {text}" : null;
}
