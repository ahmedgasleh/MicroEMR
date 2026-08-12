using MicroEMR.Application.ClinicalOutput;
using Xunit;
using MicroEMR.Infrastructure.ClinicalOutput;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicroEMR.Api.Tests;

public sealed class ClinicalPdfPreviewTests
{
    [Fact]
    public async Task PlaywrightRenderer_ProducesPdfBytes()
    {
        await using var renderer = new PlaywrightPdfRenderer(NullLogger<PlaywrightPdfRenderer>.Instance);
        var pdf = await renderer.RenderAsync("<!DOCTYPE html><html><body><h1>Preview</h1></body></html>");
        Assert.True(pdf.Length > 100);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
    }
    [Fact]
    public void PrintLayout_UsesLetterStaticHtmlAndSafeTitle()
    {
        var html = new ClinicalPrintLayoutRenderer().Render(Context(title: "Title <script>x</script>"),
            "<article class=\"template-document\">safe</article>");

        Assert.StartsWith("<!DOCTYPE html>", html.TrimStart(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@page { size: Letter; margin: 0.65in; }", html);
        Assert.Contains("break-inside: avoid", html);
        Assert.Contains("Title &lt;script&gt;x&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PatientDocumentLayout_IncludesAuthoritativeClinicalContextAndOmitsMissingOptionalValues()
    {
        var html = new ClinicalPrintLayoutRenderer().Render(Context(), "<article>Clinical body</article>");
        Assert.Contains("MicroEMR Clinic", html);
        Assert.Contains("Patient:</dt><dd>John Smith", html);
        Assert.Contains("DOB:</dt><dd>January 5, 1970", html);
        Assert.Contains("Health Card:</dt><dd>1234 AB", html);
        Assert.Contains("Chart:</dt><dd>C100", html);
        Assert.Contains("Document:</dt><dd>Consultation", html);
        Assert.Contains("Created by:</dt><dd>Dr. Author", html);
        Assert.Contains("Clinical body", html);
        Assert.DoesNotContain("Fax:", html);
    }

    [Fact]
    public void SignedEncounterLayout_UsesSignatureFooterAndEncodesAllMetadata()
    {
        var context = Context(title: "Encounter <script>x</script>") with
        {
            Clinic = Context().Clinic with { Name = "Clinic <img src=x>" },
            Patient = Context().Patient with { FullName = "Jane <b>Patient</b>" },
            Record = new("Encounter", "Encounter <script>x</script>", "Office Visit", new(2026,8,12,18,0,0,DateTimeKind.Utc), "Dr <X>"),
            Authorship = new("Prepared by", "Creator", new(2026,8,12,17,0,0,DateTimeKind.Utc), "Dr. Signer <Y>", new(2026,8,12,18,30,0,DateTimeKind.Utc))
        };
        var html = new ClinicalPrintLayoutRenderer().Render(context, "<article>Body</article>");
        Assert.Contains("Electronically signed by:</dt><dd>Dr. Signer &lt;Y&gt;", html);
        Assert.Contains("Signed:</dt><dd>", html);
        Assert.DoesNotContain("Prepared by:</dt>", html);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Jane &lt;b&gt;Patient&lt;/b&gt;", html);
    }

    [Fact]
    public void PreviewImplementation_HasNoPersistenceOrArbitrarySchemaInput()
    {
        var root = Root();
        var service = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Application", "ClinicalOutput", "ClinicalPdfPreviewService.cs"));
        Assert.Contains("GetByUidAsync(documentUid", service);
        Assert.Contains("GetByUidAsync(encounterUid", service);
        Assert.Contains("GetByUidAsync(document.TemplateVersionUid.Value", service);
        Assert.Contains("GetByUidAsync(encounter.TemplateVersionUid.Value", service);
        Assert.Contains("record TemplatePreviewRequest(string? StructuredDataJson)", service);
        Assert.DoesNotContain("UpdateDraftAsync", service);
        Assert.DoesNotContain("UpdateStructuredDataAsync", service);
        Assert.DoesNotContain("SignAsync", service);
    }

    [Fact]
    public void PopupPreview_UsesBlobRefreshHideAndCleanup()
    {
        var root = Root();
        var documentScript = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Web", "ClientApp", "patient-documents", "runtime.ts"));
        var encounterView = File.ReadAllText(Path.Combine(root, "src", "MicroEMR.Web", "Views", "Patients", "Details.cshtml"));
        Assert.Contains("URL.createObjectURL", documentScript);
        Assert.Contains("URL.revokeObjectURL", documentScript);
        Assert.Contains("Refresh Preview", documentScript);
        Assert.Contains("hideDocumentPreviewButton", documentScript);
        Assert.Contains("form.dataset.documentUid", documentScript);
        Assert.Contains("RequestVerificationToken", documentScript);
        Assert.Contains("URL.createObjectURL", encounterView);
        Assert.Contains("URL.revokeObjectURL", encounterView);
        Assert.Contains("modal-xl", encounterView);
        Assert.Contains("hideEncounterPreviewButton", encounterView);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MicroEMR.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private static ClinicalPrintContext Context(string title = "Consultation") => new(
        new("MicroEMR Clinic", "123 Main", null, "Toronto", "Ontario", "M1M 1M1", "416-555-1234", null, null),
        new("John Smith", new(1970, 1, 5), "1234", "AB", "C100"),
        new("Document", title, "Consultation", new(2026, 8, 12, 18, 15, 0, DateTimeKind.Utc), null),
        new("Created by", "Dr. Author", new(2026, 8, 12, 18, 15, 0, DateTimeKind.Utc), null, null),
        "America/Toronto");
}
