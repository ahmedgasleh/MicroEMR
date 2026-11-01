using System.Buffers.Binary;
using System.Net;
using System.Text.Json;
using MicroEMR.Application.ClinicalOutput;
using MicroEMR.Application.ClinicConfiguration;

namespace MicroEMR.Application.PatientPrescriptions;

// Version 3 contains a frozen header, never a reference to current clinic settings.
public static class PrescriptionBrandingSnapshot
{
    public static PrescriptionPdfLayout Compose(JsonElement snapshot, PrescriptionOutputOccurrence occurrence)
    {
        if (snapshot.GetProperty("PrescriptionOutputVersion").GetInt32() != 3)
            throw new InvalidOperationException("A version-3 prescription snapshot is required.");
        var branding = snapshot.GetProperty("Branding");
        if (branding.GetProperty("Version").GetInt32() != 1)
            throw new InvalidOperationException("Unsupported frozen prescription branding version.");
        string? Text(string key) => branding.GetProperty(key).ValueKind == JsonValueKind.Null
            ? null : branding.GetProperty(key).GetString();
        var name = Text("Name");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            throw new InvalidOperationException("Frozen clinic identity is missing or invalid.");
        ClinicLogo? logo = null;
        var encoded = Text("PngBase64");
        if (encoded is not null)
        {
            if (encoded.Length > ((ClinicBrandingService.MaximumLogoBytes + 2) / 3) * 4)
                throw new InvalidOperationException("Frozen clinic logo exceeds the supported limit.");
            var bytes = Convert.FromBase64String(encoded);
            var width = branding.GetProperty("Width").GetInt32();
            var height = branding.GetProperty("Height").GetInt32();
            if (bytes.Length < 45 || bytes.Length > ClinicBrandingService.MaximumLogoBytes ||
                !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
                !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) ||
                !bytes.AsSpan(bytes.Length - 8, 4).SequenceEqual("IEND"u8) ||
                width is < 1 or > ClinicBrandingService.MaximumLogoDimension ||
                height is < 1 or > ClinicBrandingService.MaximumLogoDimension ||
                BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) != width ||
                BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)) != height)
                throw new InvalidOperationException("Frozen clinic logo is not a bounded PNG.");
            logo = new(bytes, width, height);
        }
        else if (branding.GetProperty("Width").ValueKind != JsonValueKind.Null ||
                 branding.GetProperty("Height").ValueKind != JsonValueKind.Null)
            throw new InvalidOperationException("Frozen text-only branding contains inconsistent logo dimensions.");

        var header = new ClinicPrintHeader(new(name, Text("AddressLine1"), Text("AddressLine2"),
            Text("City"), Text("Province"), Text("PostalCode"), Text("Phone"), Text("Fax"), Text("Email")),
            "Prescription", logo, Text("Country"));
        var contentVersion = snapshot.GetProperty("PrescriptionContentVersion").GetInt32();
        if (contentVersion == 2 && (!snapshot.TryGetProperty("SignatureVersion", out var signature) || signature.GetInt32() != 1))
            throw new InvalidOperationException("Frozen signed prescription metadata is missing.");
        var existing = PrescriptionOutputService.Compose(snapshot, occurrence);
        var body = contentVersion switch
        {
            2 => existing.Body,
            1 when !snapshot.TryGetProperty("SignatureVersion", out _) => PatientPrescriptionService.ComposeArtifactHtml(snapshot),
            _ => throw new InvalidOperationException("Unsupported prescription clinical layout version.")
        };
        // Only the new version moves its existing title into the shared repeated header.
        body = body.Replace("<h1>Prescription</h1>", "", StringComparison.Ordinal);
        var patientHeader = contentVersion == 2
            ? existing.Header[(existing.Header.IndexOf("<b>Patient:", StringComparison.Ordinal))..]
            : "";
        // The shared fragment has prescription-local styling: its global body/@page CSS is not imported.
        var html = "<style>.clinic-letterhead{font:10px/1.35 Arial;overflow-wrap:anywhere}" +
            ".clinic-letterhead-identity{display:flex;gap:12px;align-items:center;border-bottom:1px solid #888;padding-bottom:6px}" +
            ".clinic-letterhead-logo{max-width:110px;max-height:52px;width:auto;height:auto;object-fit:contain;flex:none}" +
            ".clinic-letterhead-name{font-size:14px;font-weight:bold}.clinic-letterhead-title{font-size:13px;margin:6px 0}" +
            "</style><div style='font:9px Arial;flex:1;min-width:0;margin:0 0.65in;overflow-wrap:anywhere'>" +
            ClinicPrintHeaderRenderer.Render(header) + patientHeader;
        if (contentVersion == 1)
        {
            html += "<b>Patient: " + WebUtility.HtmlEncode(snapshot.GetProperty("PatientName").GetString()) +
                "</b> · DOB: " + WebUtility.HtmlEncode(snapshot.GetProperty("DateOfBirth").ToString()) + "</div>";
            body = body.Replace("<p>Local structured prescription. Not electronically transmitted.</p>", "", StringComparison.Ordinal);
        }
        var footer = contentVersion == 2 ? existing.Footer :
            "<div style='font:9px Arial;flex:1;min-width:0;margin:0 0.65in;line-height:1.4;overflow-wrap:anywhere'>" +
            "No electronic signature recorded<br>Print/reprint output requested by: " +
            WebUtility.HtmlEncode(occurrence.Requester.Name) + " (" + WebUtility.HtmlEncode(occurrence.Requester.Subject) + ")<br>" +
            $"Output generated at: {occurrence.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss.fffffff} UTC · Reference: {occurrence.Uid}<br>" +
            "Local structured prescription. Not electronically transmitted.<br>Generated output; physical printing not confirmed. · Page " +
            "<span class='pageNumber'></span> of <span class='totalPages'></span></div>";
        return new(body, html, footer) { MeasureHeader = true };
    }
}
